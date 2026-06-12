using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddPayOsWebhookLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payos_webhook_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_code = table.Column<string>(type: "text", nullable: true),
                    event_code = table.Column<string>(type: "text", nullable: true),
                    event_description = table.Column<string>(type: "text", nullable: true),
                    raw_payload = table.Column<string>(type: "text", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false),
                    processing_error = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payos_webhook_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payos_webhook_logs_is_processed_received_at",
                table: "payos_webhook_logs",
                columns: new[] { "is_processed", "received_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payos_webhook_logs_order_code",
                table: "payos_webhook_logs",
                column: "order_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payos_webhook_logs");
        }
    }
}
