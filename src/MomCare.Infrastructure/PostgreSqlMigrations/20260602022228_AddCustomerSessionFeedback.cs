using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddCustomerSessionFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customer_note",
                table: "package_session_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_rating",
                table: "package_session_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "customer_reviewed_at",
                table: "package_session_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_session_note",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_session_rating",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "customer_session_reviewed_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_package_session_logs_customer_rating",
                table: "package_session_logs",
                sql: "\"customer_rating\" IS NULL OR (\"customer_rating\" >= 1 AND \"customer_rating\" <= 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_bookings_customer_session_rating",
                table: "bookings",
                sql: "\"customer_session_rating\" IS NULL OR (\"customer_session_rating\" >= 1 AND \"customer_session_rating\" <= 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_package_session_logs_customer_rating",
                table: "package_session_logs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_bookings_customer_session_rating",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "customer_note",
                table: "package_session_logs");

            migrationBuilder.DropColumn(
                name: "customer_rating",
                table: "package_session_logs");

            migrationBuilder.DropColumn(
                name: "customer_reviewed_at",
                table: "package_session_logs");

            migrationBuilder.DropColumn(
                name: "customer_session_note",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "customer_session_rating",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "customer_session_reviewed_at",
                table: "bookings");
        }
    }
}
