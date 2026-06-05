using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddCustomerSessionFeedbackTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customer_tags_json",
                table: "package_session_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_session_tags_json",
                table: "bookings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "customer_tags_json",
                table: "package_session_logs");

            migrationBuilder.DropColumn(
                name: "customer_session_tags_json",
                table: "bookings");
        }
    }
}
