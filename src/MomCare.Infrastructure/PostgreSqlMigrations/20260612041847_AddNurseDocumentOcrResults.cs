using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MomCare.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddNurseDocumentOcrResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nurse_document_ocr_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nurse_document_id = table.Column<int>(type: "integer", nullable: false),
                    document_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    raw_ocr_text = table.Column<string>(type: "text", nullable: false),
                    parsed_data_json = table.Column<string>(type: "text", nullable: false),
                    ocr_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    warnings_json = table.Column<string>(type: "text", nullable: false),
                    processed_by = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nurse_document_ocr_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_nurse_document_ocr_results_documents_nurse_document_id",
                        column: x => x.nurse_document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nurse_document_ocr_results_nurse_document_id_processed_at",
                table: "nurse_document_ocr_results",
                columns: new[] { "nurse_document_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_nurse_document_ocr_results_ocr_status",
                table: "nurse_document_ocr_results",
                column: "ocr_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nurse_document_ocr_results");
        }
    }
}
