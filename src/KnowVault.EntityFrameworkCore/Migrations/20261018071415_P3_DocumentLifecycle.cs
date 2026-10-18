using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowVault.Migrations
{
    /// <inheritdoc />
    public partial class P3_DocumentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IngestAttemptCount",
                table: "AppDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppKnowledgeBaseMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeBaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppKnowledgeBaseMembers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeBaseMembers_KnowledgeBaseId_UserId",
                table: "AppKnowledgeBaseMembers",
                columns: new[] { "KnowledgeBaseId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeBaseMembers_UserId",
                table: "AppKnowledgeBaseMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppKnowledgeBaseMembers");

            migrationBuilder.DropColumn(
                name: "IngestAttemptCount",
                table: "AppDocuments");
        }
    }
}
