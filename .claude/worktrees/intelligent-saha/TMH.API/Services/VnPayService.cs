using Microsoft.EntityFrameworkCore;
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

        public async Task<string> CreatePaymentUrl(HttpContext context, VnPaymentRequestDto model)
        {
            // TxnRef chỉ dùng chữ số — VNPay không chấp nhận dấu gạch ngang
            var txnRef = $"{model.AppointmentId}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // Lưu Payment Pending
            var existing = await _db.Payments
                .FirstOrDefaultAsync(p => p.AppointmentId == model.AppointmentId
                                       && p.Status == PaymentStatus.Pending);
            if (existing != null)
            {
                existing.OrderRef = txnRef;
                existing.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
                existing.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.Payments.Add(new Payment
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
            await _db.SaveChangesAsync();

            var vnpay = new VnPayLibrary();
            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", _config["VnPay:TmnCode"]!);
            vnpay.AddRequestData("vnp_Amount", ((long)(model.Amount * 100)).ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", VnPayLibrary.GetIpAddress(context));
            vnpay.AddRequestData("vnp_Locale", "vn");
            // OrderInfo không dấu cách, không ký tự đặc biệt
            vnpay.AddRequestData("vnp_OrderInfo", $"ThanhToanLichKham{model.AppointmentId}");
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", _config["VnPay:ReturnUrl"]!);
            vnpay.AddRequestData("vnp_TxnRef", txnRef);

            return vnpay.CreateRequestUrl(_config["VnPay:BaseUrl"]!, _config["VnPay:HashSecret"]!);
        }

        public async Task<VnPaymentResponseDto> PaymentExecute(IQueryCollection collections)
        {
            var vnpay = new VnPayLibrary();
            foreach (var (key, value) in collections)
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    vnpay.AddResponseData(key, value!);

            var vnpSecureHash = collections["vnp_SecureHash"].ToString();
            var responseCode = collections["vnp_ResponseCode"].ToString();
            var txnRef = collections["vnp_TxnRef"].ToString();
            var transactionId = collections["vnp_TransactionNo"].ToString();
            var bankCode = collections["vnp_BankCode"].ToString();
            var orderInfo = collections["vnp_OrderInfo"].ToString();

            if (!vnpay.ValidateSignature(vnpSecureHash, _config["VnPay:HashSecret"]!))
                return new VnPaymentResponseDto { Success = false, Message = "Chữ ký không hợp lệ" };

            var payment = await _db.Payments
                .Include(p => p.Appointment)
                .FirstOrDefaultAsync(p => p.OrderRef == txnRef);

            if (payment == null)
                return new VnPaymentResponseDto { Success = false, Message = "Không tìm thấy giao dịch" };

            if (payment.Status == PaymentStatus.Success)
                return new VnPaymentResponseDto
                {
                    Success = true,
                    Message = "Giao dịch đã được xử lý",
                    TransactionId = payment.TransactionId ?? "",
                    OrderId = txnRef,
                    VnPayResponseCode = responseCode
                };

            if (responseCode == "00")
            {
                payment.Status = PaymentStatus.Success;
                payment.TransactionId = transactionId;
                payment.BankCode = bankCode;
                payment.VnpayResponseCode = responseCode;
                payment.PaidAt = DateTime.UtcNow;
                if (payment.Appointment != null)
                    payment.Appointment.Status = AppointmentStatus.DaXacNhan;
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
                payment.VnpayResponseCode = responseCode;
            }

            await _db.SaveChangesAsync();

            return new VnPaymentResponseDto
            {
                Success = responseCode == "00",
                Message = responseCode == "00" ? "Thanh toán thành công" : "Thanh toán thất bại",
                OrderDescription = orderInfo,
                OrderId = txnRef,
                TransactionId = transactionId,
                Token = vnpSecureHash,
                VnPayResponseCode = responseCode
            };
        }
    }
}