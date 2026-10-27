using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SupportCommunityPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HiddenByAdmin",
                schema: "forum",
                table: "Posts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "forum",
                table: "Posts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityMediaCleanup",
                schema: "forum",
                columns: table => new
                {
                    PublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DeleteAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMediaCleanup", x => x.PublicId);
                    table.CheckConstraint("CK_CommunityMediaCleanup_Type", "[MediaType] IN ('IMAGE','VIDEO')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMediaCleanup_DeleteAfter",
                schema: "forum",
                table: "CommunityMediaCleanup",
                column: "DeleteAfter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [forum].[Posts] WITH (TABLOCKX,HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[CommunityMediaCleanup] WITH (TABLOCKX,HOLDLOCK))
                    THROW 51009, 'Publishing rollback requires empty Posts and cleanup queue to preserve moderation and cleanup state.', 1;
                """);
            migrationBuilder.DropTable(
                name: "CommunityMediaCleanup",
                schema: "forum");

            migrationBuilder.DropColumn(
                name: "HiddenByAdmin",
                schema: "forum",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "forum",
                table: "Posts");
        }
    }
}
