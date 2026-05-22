using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MomCare.Data;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    [DbContext(typeof(MomCareContext))]
    [Migration("20260522093000_AddHealthRiskScoring")]
    public partial class AddHealthRiskScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "confidence_score",
                table: "ai_health_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "risk_factors_json",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "risk_score",
                table: "ai_health_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "trend_signals_json",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confidence_score",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "risk_factors_json",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "risk_score",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "trend_signals_json",
                table: "ai_health_analyses");
        }
    }
}
