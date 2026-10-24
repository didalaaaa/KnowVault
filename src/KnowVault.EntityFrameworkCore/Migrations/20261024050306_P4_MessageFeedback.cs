using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowVault.Migrations
{
    /// <inheritdoc />
    public partial class P4_MessageFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LatencyMs",
                table: "AppMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppMessageFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMessageFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppMessageFeedbacks_MessageId",
                table: "AppMessageFeedbacks",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMessageFeedbacks_MessageId_UserId",
                table: "AppMessageFeedbacks",
                columns: new[] { "MessageId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppMessageFeedbacks");

            migrationBuilder.DropColumn(
                name: "LatencyMs",
                table: "AppMessages");
        }
    }
}
