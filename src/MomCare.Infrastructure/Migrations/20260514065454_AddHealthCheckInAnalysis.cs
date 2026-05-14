using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthCheckInAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_checkins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    sleep_hours = table.Column<double>(type: "float", nullable: false),
                    pain_level = table.Column<int>(type: "int", nullable: false),
                    mood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    milk_status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    baby_feeding = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    baby_sleep = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_checkins", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_checkins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_health_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    health_checkin_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    warning_level = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    recommendations_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    suggested_services_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    raw_ai_response = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_health_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_health_analyses_health_checkins_health_checkin_id",
                        column: x => x.health_checkin_id,
                        principalTable: "health_checkins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_health_analyses_health_checkin_id",
                table: "ai_health_analyses",
                column: "health_checkin_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_health_checkins_user_id_created_at",
                table: "health_checkins",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_health_analyses");

            migrationBuilder.DropTable(
                name: "health_checkins");
        }
    }
}
