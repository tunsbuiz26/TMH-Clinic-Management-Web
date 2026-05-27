using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TMH.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsReturnToAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "Appointments");
        }
    }
}
