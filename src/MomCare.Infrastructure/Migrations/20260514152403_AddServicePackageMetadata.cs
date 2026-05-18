using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePackageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "included_service_keys",
                table: "services",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "package_days",
                table: "services",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_kind",
                table: "services",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "single");

            migrationBuilder.CreateIndex(
                name: "IX_services_service_kind",
                table: "services",
                column: "service_kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_services_service_kind",
                table: "services");

            migrationBuilder.DropColumn(
                name: "included_service_keys",
                table: "services");

            migrationBuilder.DropColumn(
                name: "package_days",
                table: "services");

            migrationBuilder.DropColumn(
                name: "service_kind",
                table: "services");
        }
    }
}
