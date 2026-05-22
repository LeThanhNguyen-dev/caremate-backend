using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MomCare.Data;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    [DbContext(typeof(MomCareContext))]
    [Migration("20260522090000_AddProfessionalHealthAiOutput")]
    public partial class AddProfessionalHealthAiOutput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "care_plan_json",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "trend_summary",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "care_plan_json",
                table: "ai_health_analyses");

            migrationBuilder.DropColumn(
                name: "trend_summary",
                table: "ai_health_analyses");
        }
    }
}
