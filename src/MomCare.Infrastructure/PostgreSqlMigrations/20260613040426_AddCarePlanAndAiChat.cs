using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddCarePlanAndAiChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_care_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    booking_id = table.Column<int>(type: "integer", nullable: true),
                    health_checkin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    plan_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    safety_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    safety_notice = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: false),
                    recommended_services_json = table.Column<string>(type: "text", nullable: false),
                    plan_items_json = table.Column<string>(type: "text", nullable: false),
                    recommended_nurses_json = table.Column<string>(type: "text", nullable: false),
                    disclaimer = table.Column<string>(type: "text", nullable: false),
                    ai_model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    fallback_mode = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_care_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_care_plans_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_care_plans_health_checkins_health_checkin_id",
                        column: x => x.health_checkin_id,
                        principalTable: "health_checkins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_care_plans_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_chat_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_conversations", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_chat_conversations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    safety_flag = table.Column<bool>(type: "boolean", nullable: false),
                    safety_triggered_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fallback_mode = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_chat_messages_ai_chat_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ai_chat_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_care_plans_booking_id",
                table: "ai_care_plans",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_care_plans_health_checkin_id",
                table: "ai_care_plans",
                column: "health_checkin_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_care_plans_user_id_status_created_at",
                table: "ai_care_plans",
                columns: new[] { "user_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_conversations_user_id_status_last_message_at",
                table: "ai_chat_conversations",
                columns: new[] { "user_id", "status", "last_message_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_conversation_id_created_at",
                table: "ai_chat_messages",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_role_created_at",
                table: "ai_chat_messages",
                columns: new[] { "role", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_care_plans");

            migrationBuilder.DropTable(
                name: "ai_chat_messages");

            migrationBuilder.DropTable(
                name: "ai_chat_conversations");
        }
    }
}
