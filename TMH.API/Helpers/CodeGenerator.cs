using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using TMH.API.Data;
using TMH.Shared.Models;

namespace TMH.API.Helpers
{
    /// <summary>
    /// Lớp tiện ích sinh mã định danh duy nhất, an toàn với tình huống tương tranh (Race Condition).
    ///
    /// VẤN ĐỀ VỚI CÁCH CŨ (Count-then-Insert):
    ///   1. Thread A đếm được count = 5  →  chuẩn bị tạo mã "BN-2026-0006"
    ///   2. Thread B đếm được count = 5  →  chuẩn bị tạo mã "BN-2026-0006"  (trùng!)
    ///   3. Thread A Insert  →  thành công
    ///   4. Thread B Insert  →  vi phạm UNIQUE constraint → lỗi hoặc dữ liệu trùng
    ///
    /// GIẢI PHÁP (Counter Table + Serializable Transaction):
    ///   - Mỗi lần cấp mã: mở Transaction Serializable → đọc + tăng NextValue → Commit
    ///   - IsolationLevel.Serializable đảm bảo SQL Server khóa hàng counter lại,
    ///     buộc các transaction khác phải chờ → loại bỏ hoàn toàn race condition
    ///   - Retry tối đa 3 lần để xử lý deadlock hoặc unique violation còn sót
    /// </summary>
    public static class CodeGenerator
    {
        // =====================================================================
        // PUBLIC API — Gọi các hàm này từ Service
        // =====================================================================

        /// <summary>Sinh mã hồ sơ bệnh nhân: BN-{năm}-{0001}</summary>
        public static Task<string> NextPatientRecordCodeAsync(AppDbContext db)
            => NextAsync(db, scope: "PATIENT_RECORD", prefix: "BN", padding: 4);

        /// <summary>Sinh mã lịch khám: APT-{năm}-{00001}</summary>
        public static Task<string> NextAppointmentCodeAsync(AppDbContext db)
            => NextAsync(db, scope: "APPOINTMENT_BOOKING", prefix: "APT", padding: 5);

        // =====================================================================
        // CORE ALGORITHM
        // =====================================================================

        /// <summary>
        /// Cấp phát số thứ tự tiếp theo từ bảng CodeCounters một cách an toàn.
        ///
        /// Luồng hoạt động:
        ///   1. Mở Transaction IsolationLevel.Serializable
        ///   2. Đọc row counter (Scope + Year) — SQL Server đặt lock trên row này
        ///   3. Tăng NextValue và SaveChanges (vẫn trong transaction)
        ///   4. Commit → lock được giải phóng
        ///   5. Trả về mã được định dạng
        ///
        /// Nếu thất bại do Unique Constraint Violation: Rollback → Clear tracker → Retry
        /// </summary>
        private static Task<string> NextAsync(
            AppDbContext db,
            string scope,
            string prefix,
            int padding)
        {
            // SqlServerRetryingExecutionStrategy không cho phép mở transaction thủ công
            // bên ngoài ExecuteAsync — phải bọc toàn bộ logic vào execution strategy.
            var strategy = db.Database.CreateExecutionStrategy();

            return strategy.ExecuteAsync(async () =>
            {
                var year = DateTime.Now.Year;

                for (var attempt = 0; attempt < 3; attempt++)
                {
                    IDbContextTransaction? transaction = null;

                    try
                    {
                        transaction = await db.Database.BeginTransactionAsync(
                            IsolationLevel.Serializable);

                        // Đọc counter — SQL Server sẽ đặt Serializable lock trên row này
                        var counter = await db.CodeCounters
                            .SingleOrDefaultAsync(c => c.Scope == scope && c.Year == year);

                        int sequenceNumber;

                        if (counter == null)
                        {
                            // Năm mới: khởi tạo bộ đếm từ 1
                            sequenceNumber = 1;
                            db.CodeCounters.Add(new CodeCounter
                            {
                                Scope     = scope,
                                Year      = year,
                                NextValue = 2   // Số tiếp theo sau khi cấp số 1
                            });
                        }
                        else
                        {
                            // Lấy số hiện tại rồi tăng lên 1 cho lần cấp tiếp theo
                            sequenceNumber   = counter.NextValue;
                            counter.NextValue++;
                        }

                        await db.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Định dạng mã: BN-2026-0001 hoặc APT-2026-00001
                        return $"{prefix}-{year}-{sequenceNumber.ToString($"D{padding}")}";
                    }
                    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < 2)
                    {
                        // Trường hợp hiếm: 2 transaction cùng INSERT counter row mới đồng thời
                        // → Rollback và thử lại, lần sau sẽ thấy row đã tồn tại và dùng UPDATE
                        if (transaction != null)
                            await transaction.RollbackAsync();

                        db.ChangeTracker.Clear();
                    }
                    catch
                    {
                        if (transaction != null)
                            await transaction.RollbackAsync();

                        throw;
                    }
                }

                throw new InvalidOperationException(
                    $"Không thể cấp mã cho scope '{scope}' sau 3 lần thử. " +
                    "Vui lòng kiểm tra database.");
            });
        }

        // =====================================================================
        // HELPER
        // =====================================================================

        /// <summary>
        /// Kiểm tra xem exception có phải do vi phạm ràng buộc UNIQUE không.
        /// SQL Server trả về error number 2601 (duplicate key row) hoặc 2627 (unique constraint).
        /// </summary>
        private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
            ex.InnerException is SqlException sqlEx &&
            (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }
}
