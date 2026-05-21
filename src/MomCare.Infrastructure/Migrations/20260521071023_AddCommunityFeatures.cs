using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_posts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    author_id = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_posts", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_posts_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "community_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    post_id = table.Column<int>(type: "int", nullable: false),
                    author_id = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_comments_community_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "community_post_likes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    post_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_post_likes", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_post_likes_community_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_post_likes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_comments_author_id",
                table: "community_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_comments_post_id_created_at",
                table: "community_comments",
                columns: new[] { "post_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_community_post_likes_post_id_user_id",
                table: "community_post_likes",
                columns: new[] { "post_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_post_likes_user_id",
                table: "community_post_likes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_author_id",
                table: "community_posts",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_is_deleted_created_at",
                table: "community_posts",
                columns: new[] { "is_deleted", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_comments");

            migrationBuilder.DropTable(
                name: "community_post_likes");

            migrationBuilder.DropTable(
                name: "community_posts");
        }
    }
}
