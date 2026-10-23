using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowVault.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSourceFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "AppDocuments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_KnowledgeBaseId_SourceFileName",
                table: "AppDocuments",
                columns: new[] { "KnowledgeBaseId", "SourceFileName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_KnowledgeBaseId_SourceFileName",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "AppDocuments");
        }
    }
}
