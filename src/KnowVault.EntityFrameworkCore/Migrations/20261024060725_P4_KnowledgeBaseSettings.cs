using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowVault.Migrations
{
    /// <inheritdoc />
    public partial class P4_KnowledgeBaseSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkOverlapChars",
                table: "AppKnowledgeBases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkSizeChars",
                table: "AppKnowledgeBases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "AppKnowledgeBases",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TopK",
                table: "AppKnowledgeBases",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkOverlapChars",
                table: "AppKnowledgeBases");

            migrationBuilder.DropColumn(
                name: "ChunkSizeChars",
                table: "AppKnowledgeBases");

            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "AppKnowledgeBases");

            migrationBuilder.DropColumn(
                name: "TopK",
                table: "AppKnowledgeBases");
        }
    }
}
