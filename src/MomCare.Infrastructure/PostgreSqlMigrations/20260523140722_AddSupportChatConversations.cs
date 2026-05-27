using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddSupportChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_conversations_bookings_booking_id",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_conversations_user1_id",
                table: "conversations");

            migrationBuilder.AlterColumn<int>(
                name: "booking_id",
                table: "conversations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "conversations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "booking");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_user1_id_user2_id_type_booking_id",
                table: "conversations",
                columns: new[] { "user1_id", "user2_id", "type", "booking_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_bookings_booking_id",
                table: "conversations",
                column: "booking_id",
                principalTable: "bookings",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_conversations_bookings_booking_id",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_conversations_user1_id_user2_id_type_booking_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "type",
                table: "conversations");

            migrationBuilder.AlterColumn<int>(
                name: "booking_id",
                table: "conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_user1_id",
                table: "conversations",
                column: "user1_id");

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_bookings_booking_id",
                table: "conversations",
                column: "booking_id",
                principalTable: "bookings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
