using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddAiChatAndCarePlanTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gemini_prompt_version",
                table: "ai_care_plans",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ai_reasoned",
                table: "ai_care_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "symptom_tags_json",
                table: "ai_care_plans",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "gemini_call_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    call_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    latency_ms = table.Column<long>(type: "bigint", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    fallback_used = table.Column<bool>(type: "boolean", nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gemini_call_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gemini_call_logs_call_type_created_at",
                table: "gemini_call_logs",
                columns: new[] { "call_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_gemini_call_logs_success_created_at",
                table: "gemini_call_logs",
                columns: new[] { "success", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gemini_call_logs");

            migrationBuilder.DropColumn(
                name: "gemini_prompt_version",
                table: "ai_care_plans");

            migrationBuilder.DropColumn(
                name: "is_ai_reasoned",
                table: "ai_care_plans");

            migrationBuilder.DropColumn(
                name: "symptom_tags_json",
                table: "ai_care_plans");
        }
    }
}
