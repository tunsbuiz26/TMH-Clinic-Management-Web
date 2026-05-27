namespace TMH.Shared.Models
{
    /// <summary>
    /// Bảng lưu số thứ tự tiếp theo cho từng loại mã định danh.
    ///
    /// Mỗi row = một "bộ đếm" riêng biệt, phân biệt theo Scope và Year.
    ///   - Scope: phạm vi chức năng (PATIENT_RECORD, APPOINTMENT_BOOKING)
    ///   - Year:  năm hiện tại — mã sẽ reset về 1 khi sang năm mới
    ///   - NextValue: số thứ tự SẼ được cấp tiếp theo (luôn tăng, không bao giờ giảm)
    ///
    /// Ví dụ sau khi cấp 3 mã bệnh nhân năm 2026:
    ///   Scope="PATIENT_RECORD", Year=2026, NextValue=4
    /// </summary>
    public class CodeCounter
    {
        public int Id { get; set; }

        /// <summary>
        /// Phạm vi chức năng: "PATIENT_RECORD" | "APPOINTMENT_BOOKING"
        /// </summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// Năm của bộ đếm — đảm bảo mã reset về 0001 mỗi năm mới.
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Số thứ tự TIẾP THEO sẽ được cấp phát.
        /// Sau mỗi lần cấp mã thành công, giá trị này tăng thêm 1.
        /// </summary>
        public int NextValue { get; set; } = 1;
    }
}
