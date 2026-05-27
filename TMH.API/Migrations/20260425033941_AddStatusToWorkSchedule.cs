using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TMH.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusToWorkSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kiểm tra cột tồn tại trước khi thêm để tránh lỗi nếu chạy lại
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[WorkSchedules]') AND name = 'Status'
                )
                BEGIN
                    ALTER TABLE [WorkSchedules] ADD [Status] nvarchar(50) NOT NULL DEFAULT 'Approved';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[WorkSchedules]') AND name = 'Status'
                )
                BEGIN
                    ALTER TABLE [WorkSchedules] DROP COLUMN [Status];
                END
            ");
        }
    }
}
