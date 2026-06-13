using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddHealthAiTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_model",
                table: "ai_health_analyses",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ai_latency_ms",
                table: "ai_health_analyses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_fallback_mode",
                table: "ai_health_analyses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "engine_version",
                table: "ai_health_analyses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: string.Empty);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_model",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "ai_latency_ms",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "ai_fallback_mode",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "engine_version",
                table: "ai_health_analyses");
        }
    }
}
