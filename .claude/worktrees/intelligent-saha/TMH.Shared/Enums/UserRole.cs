namespace TMH.Shared.Enums
{
    /// <summary>
    /// Định nghĩa 4 vai trò người dùng trong hệ thống Tai Mũi Họng.
    /// Giá trị int được lưu vào cột Role trong bảng Users của SQL Server.
    /// </summary>
    public enum UserRole
    {
        Admin   = 1,   // Quản trị viên: toàn quyền hệ thống
        Doctor  = 2,   // Bác sĩ: xem/ghi hồ sơ bệnh nhân, kết quả khám
        Staff   = 3,   // Lễ tân / Nhân viên: quản lý lịch đặt khám
        Patient = 4    // Bệnh nhân: đặt lịch, xem kết quả của chính mình
    }
}
