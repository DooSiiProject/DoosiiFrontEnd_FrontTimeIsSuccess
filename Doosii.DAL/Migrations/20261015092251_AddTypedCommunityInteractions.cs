using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTypedCommunityInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reactions_PostId_UserId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "Reactions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "LEGACY");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                schema: "forum",
                table: "Comments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "Comments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "LEGACY");

            // Resolve legacy IDs only when exactly one post table contains the ID.
            // Ambiguous rows remain LEGACY and are excluded from all typed APIs.
            migrationBuilder.Sql("""
                UPDATE c SET PostType = CASE WHEN p.Id IS NOT NULL AND s.Id IS NULL THEN 'PASS'
                    WHEN s.Id IS NOT NULL AND p.Id IS NULL THEN 'SPOT' ELSE 'LEGACY' END
                FROM forum.Comments c LEFT JOIN forum.PassPosts p ON p.Id = c.PostId
                LEFT JOIN forum.SpotSharingPosts s ON s.Id = c.PostId;
                UPDATE c SET PostType = 'LEGACY' FROM forum.Comments c
                INNER JOIN forum.Comments parent ON parent.Id = c.ParentCommentId
                WHERE c.PostId <> parent.PostId OR c.PostType <> parent.PostType;
                UPDATE r SET PostType = CASE WHEN p.Id IS NOT NULL AND s.Id IS NULL THEN 'PASS'
                    WHEN s.Id IS NOT NULL AND p.Id IS NULL THEN 'SPOT' ELSE 'LEGACY' END
                FROM forum.Reactions r LEFT JOIN forum.PassPosts p ON p.Id = r.PostId
                LEFT JOIN forum.SpotSharingPosts s ON s.Id = r.PostId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostType_PostId_UserId",
                schema: "forum",
                table: "Reactions",
                columns: new[] { "PostType", "PostId", "UserId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reactions_PostType",
                schema: "forum",
                table: "Reactions",
                sql: "[PostType] IN ('PASS','SPOT','LEGACY')");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostType_PostId_AuthorId_ClientRequestId",
                schema: "forum",
                table: "Comments",
                columns: new[] { "PostType", "PostId", "AuthorId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostType_PostId_ParentCommentId_CreatedAt_Id",
                schema: "forum",
                table: "Comments",
                columns: new[] { "PostType", "PostId", "ParentCommentId", "CreatedAt", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_PostType",
                schema: "forum",
                table: "Comments",
                sql: "[PostType] IN ('PASS','SPOT','LEGACY')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM forum.Reactions GROUP BY PostId, UserId HAVING COUNT(*) > 1)
                    THROW 51000, 'Cannot remove post type: typed reactions would collide. Resolve explicitly before rollback.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Reactions_PostType_PostId_UserId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reactions_PostType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostType_PostId_AuthorId_ClientRequestId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostType_PostId_ParentCommentId_CreatedAt_Id",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Comments_PostType",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "PostType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "PostType",
                schema: "forum",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostId_UserId",
                schema: "forum",
                table: "Reactions",
                columns: new[] { "PostId", "UserId" },
                unique: true);
        }
    }
}
