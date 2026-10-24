using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowVault.Migrations
{
    /// <inheritdoc />
    public partial class P4_ConversationContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KnowledgeBaseId",
                table: "AppConversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageAt",
                table: "AppConversations",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppConversations_CreatorId_LastMessageAt",
                table: "AppConversations",
                columns: new[] { "CreatorId", "LastMessageAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppConversations_CreatorId_LastMessageAt",
                table: "AppConversations");

            migrationBuilder.DropColumn(
                name: "KnowledgeBaseId",
                table: "AppConversations");

            migrationBuilder.DropColumn(
                name: "LastMessageAt",
                table: "AppConversations");
        }
    }
}
