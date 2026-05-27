using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace TMH.API.Services
{
    /// <summary>
    /// Gửi email qua Gmail SMTP dùng MailKit.
    /// Cấu hình trong appsettings.json:
    /// "SmtpSettings": {
    ///   "Host": "smtp.gmail.com",
    ///   "Port": 587,
    ///   "SenderEmail": "your@gmail.com",
    ///   "SenderName": "Phòng Khám TMH",
    ///   "AppPassword": "xxxx xxxx xxxx xxxx"   ← Gmail App Password (không phải mật khẩu thường)
    /// }
    /// </summary>
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ── Gửi email xác nhận đặt lịch ─────────────────────────────────
        public Task SendBookingConfirmationAsync(
            string toEmail, string toName,
            string bookingCode, string doctorName,
            string workDate, string startTime, string endTime,
            string patientName, string? note)
        {
            var subject = $"✅ Đặt lịch thành công — Mã {bookingCode}";
            var body = BookingConfirmationHtml(
                toName, bookingCode, doctorName,
                workDate, startTime, endTime, patientName, note);
            return SendAsync(toEmail, toName, subject, body);
        }

        // ── Gửi email nhắc lịch ngày mai ─────────────────────────────────
        public Task SendReminderAsync(
            string toEmail, string toName,
            string bookingCode, string doctorName,
            string workDate, string startTime, string endTime,
            string patientName)
        {
            var subject = $"🔔 Nhắc lịch khám ngày mai — {workDate}";
            var body = ReminderHtml(
                toName, bookingCode, doctorName,
                workDate, startTime, endTime, patientName);
            return SendAsync(toEmail, toName, subject, body);
        }

        // ── Gửi email thông báo huỷ lịch ─────────────────────────────────
        public Task SendCancellationAsync(
            string toEmail, string toName,
            string bookingCode, string doctorName,
            string workDate, string startTime, string endTime,
            string patientName)
        {
            var subject = $"❌ Lịch khám đã huỷ — Mã {bookingCode}";
            var body = CancellationHtml(
                toName, bookingCode, doctorName,
                workDate, startTime, endTime, patientName);
            return SendAsync(toEmail, toName, subject, body);
        }

        // ── Core: gửi email qua SMTP ──────────────────────────────────────
        private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var smtp = _config.GetSection("SmtpSettings");
            var host        = smtp["Host"]        ?? "smtp.gmail.com";
            var port        = int.Parse(smtp["Port"] ?? "587");
            var senderEmail = smtp["SenderEmail"] ?? "";
            var senderName  = smtp["SenderName"]  ?? "Phòng Khám TMH";
            var appPassword = smtp["AppPassword"] ?? "";

            if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(appPassword))
            {
                _logger.LogWarning("SmtpSettings chưa được cấu hình, bỏ qua gửi email.");
                return;
            }

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Địa chỉ email người nhận trống, bỏ qua.");
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;
                message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, appPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Đã gửi email [{Subject}] đến {Email}", subject, toEmail);
            }
            catch (Exception ex)
            {
                // Không throw — lỗi email không được ảnh hưởng luồng chính
                _logger.LogError(ex, "Lỗi gửi email đến {Email}", toEmail);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // HTML TEMPLATES
        // ══════════════════════════════════════════════════════════════════

        private static string WrapLayout(string innerHtml) => $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<style>
  *{{box-sizing:border-box;margin:0;padding:0}}
  body{{background:#f0f4f8;font-family:Arial,sans-serif;padding:32px 16px}}
  .card{{background:#fff;border-radius:16px;max-width:560px;margin:0 auto;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,.08)}}
  .header{{background:linear-gradient(135deg,#0a4d7c,#1a6fa8);padding:28px 32px;text-align:center}}
  .header-logo{{font-size:22px;font-weight:700;color:#fff;letter-spacing:.5px}}
  .header-sub{{font-size:12px;color:rgba(255,255,255,.75);margin-top:4px}}
  .body{{padding:28px 32px}}
  .greeting{{font-size:16px;color:#0a4d7c;font-weight:600;margin-bottom:18px}}
  .info-box{{background:#f0f8ff;border:1px solid #d4e8f5;border-radius:12px;padding:18px 20px;margin:18px 0}}
  .info-row{{display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid #e4f0f9;font-size:13px}}
  .info-row:last-child{{border-bottom:none}}
  .info-lbl{{color:#7a9bb0;font-weight:600}}
  .info-val{{color:#0a4d7c;font-weight:600;text-align:right}}
  .code-badge{{display:inline-block;background:#0a4d7c;color:#fff;font-size:15px;font-weight:700;padding:8px 22px;border-radius:8px;letter-spacing:1px;margin:16px 0}}
  .note-box{{background:#fff9e6;border-left:4px solid #c9a227;padding:12px 16px;border-radius:0 8px 8px 0;font-size:13px;color:#555;margin-top:12px}}
  .cta{{text-align:center;margin:22px 0}}
  .btn{{display:inline-block;background:#0a4d7c;color:#fff;text-decoration:none;padding:11px 28px;border-radius:9px;font-size:14px;font-weight:600}}
  .footer{{background:#f8fbff;padding:18px 32px;text-align:center;font-size:11px;color:#9ab0bf;border-top:1px solid #e8f0f7}}
  .footer a{{color:#1a6fa8;text-decoration:none}}
  .alert-cancel{{background:#fdecea;border:1px solid #f5c6c6;border-radius:12px;padding:16px 20px;text-align:center;margin:18px 0}}
  .alert-cancel .icon{{font-size:32px;margin-bottom:8px}}
  .alert-cancel p{{color:#c0392b;font-size:13px;font-weight:600}}
</style>
</head>
<body>
<div class='card'>
  <div class='header'>
    <div class='header-logo'>🏥 Phòng Khám Tai Mũi Họng</div>
    <div class='header-sub'>123 Đường Y Tế, Q.1, TP.HCM &nbsp;|&nbsp; Hotline: 1800 5678</div>
  </div>
  <div class='body'>{innerHtml}</div>
  <div class='footer'>
    Email này được gửi tự động từ hệ thống. Vui lòng không trả lời.<br>
    Cần hỗ trợ? Gọi <a href='tel:18005678'>1800 5678</a> hoặc email <a href='mailto:info@pktatmuihong.vn'>info@pktatmuihong.vn</a>
  </div>
</div>
</body>
</html>";

        private static string BookingConfirmationHtml(
            string toName, string bookingCode, string doctorName,
            string workDate, string startTime, string endTime,
            string patientName, string? note) => WrapLayout($@"
<div class='greeting'>Xin chào {toName},</div>
<p style='font-size:13px;color:#555;line-height:1.7;margin-bottom:16px'>
  Lịch khám của bạn đã được ghi nhận thành công. Dưới đây là thông tin chi tiết:
</p>

<div style='text-align:center'>
  <div style='font-size:12px;color:#7a9bb0;margin-bottom:4px'>Mã đặt lịch của bạn</div>
  <div class='code-badge'>{bookingCode}</div>
</div>

<div class='info-box'>
  <div class='info-row'><span class='info-lbl'>👤 Bệnh nhân</span><span class='info-val'>{patientName}</span></div>
  <div class='info-row'><span class='info-lbl'>👨‍⚕️ Bác sĩ</span><span class='info-val'>{doctorName}</span></div>
  <div class='info-row'><span class='info-lbl'>📅 Ngày khám</span><span class='info-val'>{workDate}</span></div>
  <div class='info-row'><span class='info-lbl'>🕐 Ca khám</span><span class='info-val'>{startTime} – {endTime}</span></div>
  <div class='info-row'><span class='info-lbl'>📍 Địa điểm</span><span class='info-val'>Phòng khám Tai Mũi Họng</span></div>
</div>

{(string.IsNullOrEmpty(note) ? "" : $"<div class='note-box'>📝 <strong>Ghi chú của bạn:</strong> {note}</div>")}

<p style='font-size:12px;color:#7a9bb0;margin-top:16px;line-height:1.7'>
  ⚠️ Vui lòng đến trước giờ hẹn <strong>15 phút</strong> để làm thủ tục. Mang theo mã đặt lịch khi đến khám.
</p>

<div class='cta'>
  <a class='btn' href='http://localhost:63355/Booking/MyAppointments'>Xem lịch khám của tôi</a>
</div>");

        private static string ReminderHtml(
            string toName, string bookingCode, string doctorName,
            string workDate, string startTime, string endTime,
            string patientName) => WrapLayout($@"
<div class='greeting'>Nhắc lịch: Bạn có lịch khám vào ngày mai! 🔔</div>
<p style='font-size:13px;color:#555;line-height:1.7;margin-bottom:16px'>
  Xin chào <strong>{toName}</strong>, đây là lời nhắc về lịch khám của bạn vào ngày mai.
</p>

<div class='info-box'>
  <div class='info-row'><span class='info-lbl'>📋 Mã lịch</span><span class='info-val'>{bookingCode}</span></div>
  <div class='info-row'><span class='info-lbl'>👤 Bệnh nhân</span><span class='info-val'>{patientName}</span></div>
  <div class='info-row'><span class='info-lbl'>👨‍⚕️ Bác sĩ</span><span class='info-val'>{doctorName}</span></div>
  <div class='info-row'><span class='info-lbl'>📅 Ngày khám</span><span class='info-val'>{workDate}</span></div>
  <div class='info-row'><span class='info-lbl'>🕐 Ca khám</span><span class='info-val'>{startTime} – {endTime}</span></div>
  <div class='info-row'><span class='info-lbl'>📍 Địa điểm</span><span class='info-val'>123 Đường Y Tế, Q.1, TP.HCM</span></div>
</div>

<p style='font-size:12px;color:#7a9bb0;margin-top:16px;line-height:1.7'>
  ⚠️ Vui lòng đến trước giờ hẹn <strong>15 phút</strong>. Mang theo CMND/CCCD và mã đặt lịch.<br>
  Nếu không thể đến, vui lòng huỷ lịch trên hệ thống để bác sĩ sắp xếp cho bệnh nhân khác.
</p>

<div class='cta'>
  <a class='btn' href='http://localhost:63355/Booking/MyAppointments'>Xem lịch khám của tôi</a>
</div>");

        private static string CancellationHtml(
            string toName, string bookingCode, string doctorName,
            string workDate, string startTime, string endTime,
            string patientName) => WrapLayout($@"
<div class='greeting'>Xin chào {toName},</div>

<div class='alert-cancel'>
  <div class='icon'>❌</div>
  <p>Lịch khám của bạn đã được huỷ thành công</p>
</div>

<div class='info-box'>
  <div class='info-row'><span class='info-lbl'>📋 Mã lịch</span><span class='info-val'>{bookingCode}</span></div>
  <div class='info-row'><span class='info-lbl'>👤 Bệnh nhân</span><span class='info-val'>{patientName}</span></div>
  <div class='info-row'><span class='info-lbl'>👨‍⚕️ Bác sĩ</span><span class='info-val'>{doctorName}</span></div>
  <div class='info-row'><span class='info-lbl'>📅 Ngày khám</span><span class='info-val'>{workDate}</span></div>
  <div class='info-row'><span class='info-lbl'>🕐 Ca khám</span><span class='info-val'>{startTime} – {endTime}</span></div>
</div>

<p style='font-size:13px;color:#555;line-height:1.7;margin-top:16px'>
  Nếu bạn muốn đặt lịch khám mới, hãy truy cập hệ thống và chọn khung giờ phù hợp.
</p>

<div class='cta'>
  <a class='btn' href='http://localhost:63355/Booking'>Đặt lịch khám mới</a>
</div>");
    }
}
