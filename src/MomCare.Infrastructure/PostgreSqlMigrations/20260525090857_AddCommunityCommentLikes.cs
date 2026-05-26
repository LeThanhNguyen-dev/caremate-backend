using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddCommunityCommentLikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_comment_likes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    comment_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_comment_likes", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_comment_likes_community_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "community_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_comment_likes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_comment_likes_comment_id_user_id",
                table: "community_comment_likes",
                columns: new[] { "comment_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_comment_likes_user_id",
                table: "community_comment_likes",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_comment_likes");
        }
    }
}
