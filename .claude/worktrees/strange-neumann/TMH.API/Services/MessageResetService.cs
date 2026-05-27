using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.Enums;

namespace TMH.API.Services
{
    /// <summary>
    /// Background service chạy mỗi ngày lúc 12:00 PM (trưa).
    /// Xoá tất cả tin nhắn PatientStaff để reset đoạn chat tư vấn.
    /// Tin nhắn nội bộ (Internal) được giữ nguyên.
    /// </summary>
    public class MessageResetService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MessageResetService> _logger;

        private static readonly TimeOnly RUN_AT = new TimeOnly(12, 0); // 12:00 PM

        public MessageResetService(
            IServiceScopeFactory scopeFactory,
            ILogger<MessageResetService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MessageResetService đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = DateTime.Today.Add(RUN_AT.ToTimeSpan());

                if (now > nextRun)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("Reset tin nhắn kế tiếp lúc {NextRun}", nextRun);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                    await ResetMessagesAsync(stoppingToken);
            }
        }

        private async Task ResetMessagesAsync(CancellationToken ct)
        {
            _logger.LogInformation("Đang reset tin nhắn PatientStaff...");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Lấy tất cả conversation PatientStaff
            var patientStaffConvIds = await db.Conversations
                .Where(c => c.Type == ConversationType.PatientStaff)
                .Select(c => c.Id)
                .ToListAsync(ct);

            if (!patientStaffConvIds.Any())
            {
                _logger.LogInformation("Không có conversation PatientStaff nào.");
                return;
            }

            // Xoá tất cả tin nhắn thuộc conversation PatientStaff
            var deletedCount = await db.Messages
                .Where(m => patientStaffConvIds.Contains(m.ConversationId))
                .ExecuteDeleteAsync(ct);

            _logger.LogInformation("Đã xoá {Count} tin nhắn PatientStaff.", deletedCount);

            // Reset LastMessageAt cho các conversation đã xoá tin nhắn
            await db.Conversations
                .Where(c => patientStaffConvIds.Contains(c.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.LastMessageAt, DateTime.UtcNow), ct);
        }
    }
}
