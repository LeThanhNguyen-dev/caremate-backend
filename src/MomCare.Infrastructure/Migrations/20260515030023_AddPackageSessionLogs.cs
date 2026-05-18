using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageSessionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "package_schedule_json",
                table: "services",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "package_session_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    booking_id = table.Column<int>(type: "int", nullable: false),
                    session_number = table.Column<int>(type: "int", nullable: false),
                    session_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    planned_service_keys = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    check_in_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    check_out_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    nurse_note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_session_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_package_session_logs_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_package_session_logs_booking_id_session_date",
                table: "package_session_logs",
                columns: new[] { "booking_id", "session_date" });

            migrationBuilder.CreateIndex(
                name: "IX_package_session_logs_booking_id_session_number",
                table: "package_session_logs",
                columns: new[] { "booking_id", "session_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "package_session_logs");

            migrationBuilder.DropColumn(
                name: "package_schedule_json",
                table: "services");
        }
    }
}
