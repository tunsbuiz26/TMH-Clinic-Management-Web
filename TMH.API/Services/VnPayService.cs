using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TMH.API.Data;
using TMH.API.Helpers;
using TMH.Shared.DTOs;
using TMH.Shared.Models;

namespace TMH.API.Services
{
    public class VnPayService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;

        public VnPayService(IConfiguration config, AppDbContext db)
        {
            _config = config;
            _db = db;
        }

        // ================= CREATE PAYMENT =================
        public async Task<string> CreatePaymentUrl(HttpContext context, VnPaymentRequestDto model)
        {
            // txnRef cần đủ khác nhau kể cả khi người dùng bấm lại rất nhanh.
            var txnRef = $"{model.AppointmentId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            var appointment = await _db.Appointments
                .Include(a => a.Schedule)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appointment == null)
                throw new Exception("Không tìm thấy lịch khám");

            if (appointment.Status == AppointmentStatus.DaHuy)
                throw new Exception("Lịch khám đã bị huỷ, không thể thanh toán");

            // Check đã thanh toán chưa
            var paid = await _db.Payments
                .AnyAsync(p => p.AppointmentId == model.AppointmentId
                            && p.Status == PaymentStatus.Success);

            if (paid)
                throw new Exception("Lịch này đã được thanh toán rồi");

            // Mỗi lịch khám chỉ có tối đa 1 payment record.
            // Nếu đã có payment cũ (Pending/Failed/Refunded) thì tái sử dụng,
            // tránh vi phạm unique index trên AppointmentId khi tạo link mới.
            var existing = await _db.Payments
                .FirstOrDefaultAsync(p => p.AppointmentId == model.AppointmentId);

            EntityEntry<Payment>? addedEntry = null;

            if (existing != null)
            {
                ResetPayment(existing, model, txnRef);
            }
            else
            {
                addedEntry = _db.Payments.Add(new Payment
                {
                    AppointmentId = model.AppointmentId,
                    Amount = (long)model.Amount,
                    Method = "vnpay",
                    Status = PaymentStatus.Pending,
                    OrderRef = txnRef,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                });
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsAppointmentPaymentConflict(ex))
            {
                if (addedEntry != null)
                    addedEntry.State = EntityState.Detached;

                var concurrentPayment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.AppointmentId == model.AppointmentId);

                if (concurrentPayment == null)
                    throw CreateFriendlySaveException(ex);

                ResetPayment(concurrentPayment, model, txnRef);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw CreateFriendlySaveException(ex);
            }

            // ================= TIME VIỆT NAM =================
            var vnTimeZone = GetVietnamTimeZone();
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

            var vnpay = new VnPayLibrary();

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", _config["VnPay:TmnCode"]!);

            // ⚠️ Amount * 100
            vnpay.AddRequestData("vnp_Amount", ((long)(model.Amount * 100)).ToString());

            // ✅ GIỜ VN
            vnpay.AddRequestData("vnp_CreateDate", vnTime.ToString("yyyyMMddHHmmss"));

            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", VnPayLibrary.GetIpAddress(context));
            vnpay.AddRequestData("vnp_Locale", "vn");

            // ⚠️ KHÔNG dấu, KHÔNG space
            vnpay.AddRequestData("vnp_OrderInfo", $"ThanhToanLichKham_{model.AppointmentId}");

            vnpay.AddRequestData("vnp_OrderType", "other");

            vnpay.AddRequestData("vnp_ReturnUrl", _config["VnPay:ReturnUrl"]!);

            vnpay.AddRequestData("vnp_TxnRef", txnRef);

            // ✅ HẾT HẠN = giờ VN + 15 phút
            vnpay.AddRequestData("vnp_ExpireDate",
                vnTime.AddMinutes(15).ToString("yyyyMMddHHmmss"));

            return vnpay.CreateRequestUrl(
                _config["VnPay:BaseUrl"]!,
                _config["VnPay:HashSecret"]!
            );
        }

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            var timezoneIds = new[]
            {
                "SE Asia Standard Time",
                "Asia/Ho_Chi_Minh",
                "Asia/Bangkok"
            };

            foreach (var id in timezoneIds)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.CreateCustomTimeZone(
                "Vietnam Standard Time",
                TimeSpan.FromHours(7),
                "Vietnam Standard Time",
                "Vietnam Standard Time");
        }

        private static void ResetPayment(Payment payment, VnPaymentRequestDto model, string txnRef)
        {
            payment.Amount = (long)model.Amount;
            payment.Method = "vnpay";
            payment.Status = PaymentStatus.Pending;
            payment.OrderRef = txnRef;
            payment.TransactionId = null;
            payment.BankCode = null;
            payment.VnpayResponseCode = null;
            payment.RawIpnData = null;
            payment.PaidAt = null;
            payment.CreatedAt = DateTime.UtcNow;
            payment.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
        }

        private static bool IsAppointmentPaymentConflict(DbUpdateException ex)
        {
            var fullMessage = BuildExceptionMessage(ex).ToLowerInvariant();
            return fullMessage.Contains("ix_payments_appointmentid")
                   || fullMessage.Contains("appointmentid")
                   || fullMessage.Contains("duplicate key")
                   || fullMessage.Contains("unique constraint")
                   || fullMessage.Contains("unique index");
        }

        private static Exception CreateFriendlySaveException(DbUpdateException ex)
        {
            return new Exception($"Lỗi lưu thanh toán: {BuildExceptionMessage(ex)}", ex);
        }

        private static string BuildExceptionMessage(Exception ex)
        {
            var parts = new List<string>();
            Exception? current = ex;

            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                    parts.Add(current.Message);

                current = current.InnerException;
            }

            return string.Join(" | ", parts.Distinct());
        }

        // ================= HANDLE RESPONSE =================
        public async Task<VnPaymentResponseDto> PaymentExecute(IQueryCollection collections)
        {
            var vnpay = new VnPayLibrary();

            foreach (var (key, value) in collections)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    vnpay.AddResponseData(key, value!);
            }

            string secureHash = collections["vnp_SecureHash"];
            string responseCode = collections["vnp_ResponseCode"];
            string txnRef = collections["vnp_TxnRef"];
            string transactionId = collections["vnp_TransactionNo"];
            string bankCode = collections["vnp_BankCode"];
            string orderInfo = collections["vnp_OrderInfo"];

            // ✅ VALIDATE SIGNATURE
            if (!vnpay.ValidateSignature(secureHash, _config["VnPay:HashSecret"]!))
            {
                return new VnPaymentResponseDto
                {
                    Success = false,
                    Message = "Sai chữ ký"
                };
            }

            var payment = await _db.Payments
                .Include(p => p.Appointment)
                .FirstOrDefaultAsync(p => p.OrderRef == txnRef);

            if (payment == null)
            {
                return new VnPaymentResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy giao dịch"
                };
            }

            // Nếu đã success trước đó
            if (payment.Status == PaymentStatus.Success)
            {
                return new VnPaymentResponseDto
                {
                    Success = true,
                    Message = "Đã thanh toán trước đó",
                    OrderId = txnRef,
                    TransactionId = payment.TransactionId ?? ""
                };
            }

            // ================= HANDLE RESULT =================
            if (responseCode == "00")
            {
                payment.Status = PaymentStatus.Success;
                payment.TransactionId = transactionId;
                payment.BankCode = bankCode;
                payment.VnpayResponseCode = responseCode;
                payment.PaidAt = DateTime.UtcNow;

                if (payment.Appointment != null)
                    payment.Appointment.Status = AppointmentStatus.DaXacNhan;

                var hasNotification = await _db.Notifications.AnyAsync(n =>
                    n.AppointmentId == payment.AppointmentId &&
                    n.Type == NotificationType.ThanhToan);

                if (!hasNotification && payment.Appointment != null)
                {
                    var appointment = await _db.Appointments
                        .Include(a => a.Patient)
                        .Include(a => a.Doctor)
                        .Include(a => a.Schedule)
                        .FirstOrDefaultAsync(a => a.Id == payment.AppointmentId);

                    if (appointment != null)
                    {
                        var startTime = appointment.Schedule.StartTime;

                        _db.Notifications.Add(new Notification
                        {
                            UserId = appointment.Patient.UserId,
                            AppointmentId = appointment.Id,
                            Title = "Thanh toán thành công",
                            Content = $"Bạn đã thanh toán thành công lịch khám ngày {appointment.Schedule.WorkDate:dd/MM/yyyy} lúc {startTime:hh\\:mm}. Mã số khám: {appointment.BookingCode}.",
                            Type = NotificationType.ThanhToan,
                            SentAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }
                }
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
                payment.VnpayResponseCode = responseCode;
            }

            // log raw
            payment.RawIpnData = string.Join("&",
                collections.Select(x => $"{x.Key}={x.Value}"));

            await _db.SaveChangesAsync();

            return new VnPaymentResponseDto
            {
                Success = responseCode == "00",
                Message = responseCode == "00" ? "Thanh toán thành công" : "Thanh toán thất bại",
                OrderId = txnRef,
                TransactionId = transactionId,
                OrderDescription = orderInfo,
                VnPayResponseCode = responseCode
            };
        }
    }
}
