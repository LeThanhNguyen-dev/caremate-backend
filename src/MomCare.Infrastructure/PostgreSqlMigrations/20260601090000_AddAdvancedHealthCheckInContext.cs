using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MomCare.Data;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    [DbContext(typeof(MomCareContext))]
    [Migration("20260601090000_AddAdvancedHealthCheckInContext")]
    public partial class AddAdvancedHealthCheckInContext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pain_location",
                table: "health_checkins",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pain_type",
                table: "health_checkins",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pain_duration",
                table: "health_checkins",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pain_trend",
                table: "health_checkins",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "symptoms_json",
                table: "health_checkins",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "medical_history_json",
                table: "health_checkins",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "mother_age",
                table: "health_checkins",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "systolic_blood_pressure",
                table: "health_checkins",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "diastolic_blood_pressure",
                table: "health_checkins",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "temperature_celsius",
                table: "health_checkins",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "took_medication_today",
                table: "health_checkins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "medication_note",
                table: "health_checkins",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "triage_color",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "urgency_action",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "weekly_summary",
                table: "ai_health_analyses",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "pain_location", table: "health_checkins");
            migrationBuilder.DropColumn(name: "pain_type", table: "health_checkins");
            migrationBuilder.DropColumn(name: "pain_duration", table: "health_checkins");
            migrationBuilder.DropColumn(name: "pain_trend", table: "health_checkins");
            migrationBuilder.DropColumn(name: "symptoms_json", table: "health_checkins");
            migrationBuilder.DropColumn(name: "medical_history_json", table: "health_checkins");
            migrationBuilder.DropColumn(name: "mother_age", table: "health_checkins");
            migrationBuilder.DropColumn(name: "systolic_blood_pressure", table: "health_checkins");
            migrationBuilder.DropColumn(name: "diastolic_blood_pressure", table: "health_checkins");
            migrationBuilder.DropColumn(name: "temperature_celsius", table: "health_checkins");
            migrationBuilder.DropColumn(name: "took_medication_today", table: "health_checkins");
            migrationBuilder.DropColumn(name: "medication_note", table: "health_checkins");
            migrationBuilder.DropColumn(name: "triage_color", table: "ai_health_analyses");
            migrationBuilder.DropColumn(name: "urgency_action", table: "ai_health_analyses");
            migrationBuilder.DropColumn(name: "weekly_summary", table: "ai_health_analyses");
        }
    }
}
