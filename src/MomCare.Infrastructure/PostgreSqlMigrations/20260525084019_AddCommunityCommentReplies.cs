using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddCommunityCommentReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parent_comment_id",
                table: "community_comments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_comments_parent_comment_id",
                table: "community_comments",
                column: "parent_comment_id");

            migrationBuilder.AddForeignKey(
                name: "FK_community_comments_community_comments_parent_comment_id",
                table: "community_comments",
                column: "parent_comment_id",
                principalTable: "community_comments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_community_comments_community_comments_parent_comment_id",
                table: "community_comments");

            migrationBuilder.DropIndex(
                name: "IX_community_comments_parent_comment_id",
                table: "community_comments");

            migrationBuilder.DropColumn(
                name: "parent_comment_id",
                table: "community_comments");
        }
    }
}
