using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddCareMateAiEngineV3SnapshotFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "data_coverage_items_json",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "data_coverage_percent",
                table: "ai_health_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "missing_data_items_json",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "narrative_summary",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "nutrition_guidance_json",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ppd_screening_level",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ppd_screening_note",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ppd_screening_score",
                table: "ai_health_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_coverage_items_json",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "data_coverage_percent",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "missing_data_items_json",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "narrative_summary",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "nutrition_guidance_json",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "ppd_screening_level",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "ppd_screening_note",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "ppd_screening_score",
                table: "ai_health_analyses");
        }
    }
}
