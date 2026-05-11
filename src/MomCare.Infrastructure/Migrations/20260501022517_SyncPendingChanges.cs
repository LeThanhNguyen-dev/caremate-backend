using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_booked",
                table: "availability_slots");

            migrationBuilder.RenameColumn(
                name: "file_url",
                table: "documents",
                newName: "public_id");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "reviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "reviews",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "nurse_profiles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "nurse_profiles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "documents",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "availability_slot_id",
                table: "bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_availability_slot_id",
                table: "bookings",
                column: "availability_slot_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_availability_slots_availability_slot_id",
                table: "bookings",
                column: "availability_slot_id",
                principalTable: "availability_slots",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_availability_slots_availability_slot_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_availability_slot_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "nurse_profiles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "nurse_profiles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "availability_slot_id",
                table: "bookings");

            migrationBuilder.RenameColumn(
                name: "public_id",
                table: "documents",
                newName: "file_url");

            migrationBuilder.AddColumn<bool>(
                name: "is_booked",
                table: "availability_slots",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
