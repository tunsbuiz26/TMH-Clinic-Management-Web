using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TMH.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoTenDem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NgaySinh = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GioiTinh = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NhomMau = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SoCCCD = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Degree = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Doctors_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RecordCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MedicalHistory = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    MaxPatients = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    CurrentPatients = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    BookingCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BookedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Diagnosis = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_WorkSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DiaChi", "Email", "GioiTinh", "HoTenDem", "IsActive", "IsEmailVerified", "LastLoginAt", "NgaySinh", "NhomMau", "PasswordHash", "Phone", "Role", "SoCCCD", "Ten", "Username" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin@pktatmuihong.vn", null, "Nguyễn Văn", true, true, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000001", 1, null, "Admin", "admin" },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "bsvan@pktatmuihong.vn", null, "TS.BS. Nguyễn Thanh", true, true, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000002", 2, null, "Vân", "bsvan" },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "bslinh@pktatmuihong.vn", null, "PGS.BS. Trần Minh", true, true, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000003", 2, null, "Linh", "bslinh" },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "bshuong@pktatmuihong.vn", null, "TS.BS. Phạm Thị", true, true, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000004", 2, null, "Hương", "bshuong" },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "letanmai@pktatmuihong.vn", null, "Trần Thị", true, true, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000005", 3, null, "Mai", "letanmai" },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "levan.an@gmail.com", null, "Lê Văn", true, false, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000006", 4, null, "An", "benhnhan01" },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "nguyen.binh@gmail.com", null, "Nguyễn Thị", true, true, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000007", 4, null, "Bình", "benhnhan02" },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "pham.cuong@gmail.com", null, "Phạm Văn", true, true, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000008", 4, null, "Cường", "benhnhan03" },
                    { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "hoang.dung@gmail.com", null, "Hoàng Thị", true, false, null, null, null, "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", "0901000009", 4, null, "Dung", "benhnhan04" }
                });

            migrationBuilder.InsertData(
                table: "Doctors",
                columns: new[] { "Id", "Degree", "Description", "FullName", "IsAvailable", "Specialty", "UserId" },
                values: new object[,]
                {
                    { 1, "TS", "22 năm kinh nghiệm, BV TMH TW, tốt nghiệp ĐH Tokyo", "TS.BS. Nguyễn Thanh Vân", true, "Tai Mũi Họng — Phẫu thuật nội soi xoang", 2 },
                    { 2, "PGS", "18 năm kinh nghiệm, chuyên thính học, ĐH Melbourne", "PGS.BS. Trần Minh Linh", true, "Tai Mũi Họng — Thính học & cấy ốc tai", 3 },
                    { 3, "TS", "14 năm kinh nghiệm tại BV Nhi Đồng 2, ĐH Seoul", "TS.BS. Phạm Thị Hương", true, "TMH Nhi — Viêm tai giữa trẻ em", 4 }
                });

            migrationBuilder.InsertData(
                table: "Patients",
                columns: new[] { "Id", "CreatedAt", "DateOfBirth", "FullName", "Gender", "MedicalHistory", "RecordCode", "UserId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1990, 5, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lê Văn An", "Nam", "Viêm mũi dị ứng mãn tính", "BN-2026-0001", 6 },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1985, 8, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Thị Bình", "Nữ", "Viêm amidan tái phát", "BN-2026-0002", 7 },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2000, 3, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Phạm Văn Cường", "Nam", "Không có tiền sử bệnh", "BN-2026-0003", 8 },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1978, 11, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hoàng Thị Dung", "Nữ", "Polyp mũi, đã phẫu thuật năm 2020", "BN-2026-0004", 9 },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2018, 6, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nguyễn Minh Khôi", "Nam", "Trẻ em, viêm tai giữa tái phát nhiều lần", "BN-2026-0005", 7 }
                });

            migrationBuilder.InsertData(
                table: "WorkSchedules",
                columns: new[] { "Id", "CurrentPatients", "DoctorId", "EndTime", "MaxPatients", "StartTime", "WorkDate" },
                values: new object[,]
                {
                    { 1, 3, 1, new TimeSpan(0, 11, 0, 0, 0), 10, new TimeSpan(0, 7, 0, 0, 0), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, 1, 1, new TimeSpan(0, 17, 0, 0, 0), 10, new TimeSpan(0, 13, 0, 0, 0), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, 2, 2, new TimeSpan(0, 11, 0, 0, 0), 8, new TimeSpan(0, 7, 0, 0, 0), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 4, 1, 3, new TimeSpan(0, 11, 0, 0, 0), 10, new TimeSpan(0, 7, 0, 0, 0), new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "WorkSchedules",
                columns: new[] { "Id", "DoctorId", "EndTime", "MaxPatients", "StartTime", "WorkDate" },
                values: new object[] { 5, 2, new TimeSpan(0, 11, 0, 0, 0), 8, new TimeSpan(0, 7, 0, 0, 0), new DateTime(2026, 3, 19, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.InsertData(
                table: "Appointments",
                columns: new[] { "Id", "BookedAt", "BookingCode", "Diagnosis", "DoctorId", "Note", "PatientId", "ScheduleId", "Status" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 15, 9, 0, 0, 0, DateTimeKind.Utc), "APT-2026-00001", "Viêm xoang hàm hai bên mãn tính", 1, "Khám viêm xoang mãn tính", 1, 1, 5 },
                    { 2, new DateTime(2026, 3, 16, 10, 0, 0, 0, DateTimeKind.Utc), "APT-2026-00002", null, 1, "Tái khám sau điều trị amidan", 2, 1, 2 },
                    { 3, new DateTime(2026, 3, 17, 8, 0, 0, 0, DateTimeKind.Utc), "APT-2026-00003", null, 2, "Nghe kém tai phải", 3, 3, 3 },
                    { 4, new DateTime(2026, 3, 17, 14, 0, 0, 0, DateTimeKind.Utc), "APT-2026-00004", null, 3, "Trẻ 7 tuổi, viêm tai giữa tái phát", 5, 4, 1 },
                    { 5, new DateTime(2026, 3, 18, 7, 30, 0, 0, DateTimeKind.Utc), "APT-2026-00005", null, 1, "Bệnh nhân huỷ do bận việc", 4, 2, 6 }
                });

            migrationBuilder.InsertData(
                table: "Notifications",
                columns: new[] { "Id", "AppointmentId", "Content", "IsRead", "SentAt", "Title", "Type", "UserId" },
                values: new object[,]
                {
                    { 1, 1, "Lịch khám ngày 18/03 lúc 07:00 với BS. Nguyễn Thanh Vân đã xác nhận. Mã: APT-2026-00001", true, new DateTime(2026, 3, 15, 9, 0, 0, 0, DateTimeKind.Utc), "Đặt lịch thành công", 1, 6 },
                    { 2, 1, "Bạn có lịch khám 07:00 ngày 18/03 với BS. Nguyễn Thanh Vân. Đến trước 15 phút.", true, new DateTime(2026, 3, 17, 8, 0, 0, 0, DateTimeKind.Utc), "Nhắc lịch khám ngày mai", 2, 6 },
                    { 3, 2, "Lịch tái khám ngày 18/03 lúc 07:00 đã được lễ tân xác nhận. Mã: APT-2026-00002", false, new DateTime(2026, 3, 16, 10, 5, 0, 0, DateTimeKind.Utc), "Xác nhận lịch tái khám", 1, 7 },
                    { 4, 4, "Lịch khám bé Minh Khôi ngày 18/03 lúc 07:00 với BS. Phạm Thị Hương đang chờ xác nhận.", false, new DateTime(2026, 3, 17, 14, 5, 0, 0, DateTimeKind.Utc), "Đặt lịch cho bé thành công", 1, 7 },
                    { 5, 5, "Lịch khám ngày 18/03 lúc 13:00 với BS. Nguyễn Thanh Vân đã được huỷ theo yêu cầu.", false, new DateTime(2026, 3, 18, 7, 0, 0, 0, DateTimeKind.Utc), "Lịch khám đã bị huỷ", 3, 9 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BookingCode",
                table: "Appointments",
                column: "BookingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ScheduleId",
                table: "Appointments",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_UserId",
                table: "Doctors",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AppointmentId",
                table: "Notifications",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_RecordCode",
                table: "Patients",
                column: "RecordCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId",
                table: "Patients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_DoctorId",
                table: "WorkSchedules",
                column: "DoctorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
