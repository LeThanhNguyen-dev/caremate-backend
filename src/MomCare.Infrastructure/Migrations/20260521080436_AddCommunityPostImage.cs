using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityPostImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_public_id",
                table: "community_posts",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "community_posts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_public_id",
                table: "community_posts");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "community_posts");
        }
    }
}
