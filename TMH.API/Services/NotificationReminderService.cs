using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.Models;

namespace TMH.API.Services
{
    /// <summary>
    /// NotificationReminderService là IHostedService chạy nền.
    /// Mỗi ngày lúc 08:00 sáng, nó quét các lịch khám ngày mai
    /// và tạo Notification nhắc lịch cho bệnh nhân nếu chưa có.
    ///
    /// Đăng ký bằng: builder.Services.AddHostedService&lt;NotificationReminderService&gt;()
    /// </summary>
    public class NotificationReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationReminderService> _logger;

        // Chạy lúc 08:00 sáng mỗi ngày
        private static readonly TimeOnly RUN_AT = new TimeOnly(8, 0);

        public NotificationReminderService(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationReminderService đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = DateTime.Today.Add(RUN_AT.ToTimeSpan());

                // Nếu đã qua 08:00 hôm nay thì đặt sang 08:00 ngày mai
                if (now > nextRun)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("Nhắc lịch kế tiếp lúc {NextRun}", nextRun);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                    await SendRemindersAsync(stoppingToken);
            }
        }

        private async Task SendRemindersAsync(CancellationToken ct)
        {
            _logger.LogInformation("Đang gửi thông báo nhắc lịch...");

            // Dùng IServiceScope vì DbContext là Scoped, không inject trực tiếp vào Singleton
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tomorrow = DateTime.Today.AddDays(1);

            // Lấy các lịch khám ngày mai còn hoạt động
            var appointments = await db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .Where(a =>
                    a.Schedule.WorkDate.Date == tomorrow &&
                    a.Status != AppointmentStatus.DaHuy &&
                    a.Status != AppointmentStatus.HoanThanh &&
                    a.Status != AppointmentStatus.VangMat)
                .ToListAsync(ct);

            int count = 0;
            foreach (var apt in appointments)
            {
                // Kiểm tra đã có notification nhắc lịch này chưa (tránh gửi trùng)
                bool alreadySent = await db.Notifications.AnyAsync(n =>
                    n.AppointmentId == apt.Id &&
                    n.Type == NotificationType.NhacLich, ct);

                if (alreadySent) continue;

                var notification = new Notification
                {
                    UserId = apt.Patient.UserId,
                    AppointmentId = apt.Id,
                    Title = "Nhắc lịch khám ngày mai",
                    Content = $"Bạn có lịch khám vào ngày mai " +
                                   $"{apt.Schedule.WorkDate:dd/MM/yyyy} " +
                                   $"lúc {(int)apt.Schedule.StartTime.TotalHours:D2}:{apt.Schedule.StartTime.Minutes:D2} " +
                                   $"với {apt.Doctor.FullName}. " +
                                   $"Mã lịch: {apt.BookingCode}. " +
                                   $"Vui lòng đến đúng giờ.",
                    Type = NotificationType.NhacLich,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };

                db.Notifications.Add(notification);
                count++;
            }

            if (count > 0)
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Đã tạo {Count} thông báo nhắc lịch.", count);

                // Gửi email nhắc lịch (fire-and-forget)
                var emailSvc = scope.ServiceProvider.GetRequiredService<EmailService>();
                foreach (var apt in appointments)
                {
                    bool alreadySentCheck = await db.Notifications.AnyAsync(n =>
                        n.AppointmentId == apt.Id &&
                        n.Type == NotificationType.NhacLich, ct);
                    if (!alreadySentCheck) continue; // Chỉ gửi email nếu notification vừa được tạo

                    var userEmail = apt.Patient?.User?.Email ?? "";
                    var userName  = $"{apt.Patient?.User?.HoTenDem} {apt.Patient?.User?.Ten}".Trim();

                    _ = emailSvc.SendReminderAsync(
                        userEmail, userName,
                        apt.BookingCode,
                        apt.Doctor?.FullName ?? "",
                        apt.Schedule.WorkDate.ToString("dd/MM/yyyy"),
                        $"{(int)apt.Schedule.StartTime.TotalHours:D2}:{apt.Schedule.StartTime.Minutes:D2}",
                        $"{(int)apt.Schedule.EndTime.TotalHours:D2}:{apt.Schedule.EndTime.Minutes:D2}",
                        apt.Patient?.FullName ?? ""
                    );
                }
            }
            else
            {
                _logger.LogInformation("Không có lịch khám nào cần nhắc.");
            }
        }
    }
}
