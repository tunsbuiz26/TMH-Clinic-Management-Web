using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TMH.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Bảng Prescriptions ────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    DoctorId      = table.Column<int>(type: "int", nullable: false),
                    Notes         = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IssuedAt      = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);

                    // FK → Appointments: cascade xoá lịch → xoá đơn thuốc
                    table.ForeignKey(
                        name: "FK_Prescriptions_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    // FK → Doctors: restrict — không xoá bác sĩ khi còn đơn thuốc
                    table.ForeignKey(
                        name: "FK_Prescriptions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // UNIQUE: 1 lịch khám = 1 đơn thuốc
            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_AppointmentId",
                table: "Prescriptions",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_DoctorId",
                table: "Prescriptions",
                column: "DoctorId");

            // ── Bảng PrescriptionItems ────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                columns: table => new
                {
                    Id             = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<int>(type: "int", nullable: false),
                    MedicineName   = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Dosage         = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Frequency      = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationDays   = table.Column<int>(type: "int", nullable: false),
                    Quantity       = table.Column<int>(type: "int", nullable: false),
                    Instructions   = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);

                    // FK → Prescriptions: cascade xoá đơn → xoá toàn bộ chi tiết
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_PrescriptionId",
                table: "PrescriptionItems",
                column: "PrescriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PrescriptionItems");
            migrationBuilder.DropTable(name: "Prescriptions");
        }
    }
}
