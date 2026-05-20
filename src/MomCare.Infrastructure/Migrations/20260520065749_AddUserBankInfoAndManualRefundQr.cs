using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBankInfoAndManualRefundQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_account_name",
                table: "users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_account_number",
                table: "users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_bin",
                table: "users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bank_account_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "bank_account_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "bank_bin",
                table: "users");
        }
    }
}
