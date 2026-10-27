using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowVault.Migrations
{
    /// <inheritdoc />
    public partial class P5_ConversationScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KnowledgeBaseScopeJson",
                table: "AppConversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ScopeAll",
                table: "AppConversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KnowledgeBaseScopeJson",
                table: "AppConversations");

            migrationBuilder.DropColumn(
                name: "ScopeAll",
                table: "AppConversations");
        }
    }
}
