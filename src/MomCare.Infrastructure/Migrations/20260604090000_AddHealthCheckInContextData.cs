using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthCheckInContextData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "context_data_json",
                table: "health_checkins",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "context_data_json",
                table: "health_checkins");
        }
    }
}
