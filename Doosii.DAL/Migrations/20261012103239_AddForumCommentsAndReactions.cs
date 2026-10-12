using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddForumCommentsAndReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reactions_UserId_PostType_PostId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostType_PostId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "PostType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropColumn(
                name: "PostType",
                schema: "forum",
                table: "Comments");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "forum",
                table: "Comments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "forum",
                table: "Comments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostId",
                schema: "forum",
                table: "Reactions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostId_UserId",
                schema: "forum",
                table: "Reactions",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions",
                column: "ReactionType");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_UserId",
                schema: "forum",
                table: "Reactions",
                column: "UserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions",
                sql: "[ReactionType] IN ('LIKE', 'HEART')");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId",
                schema: "forum",
                table: "Comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId",
                schema: "forum",
                table: "Comments",
                column: "PostId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                schema: "forum",
                table: "Comments",
                column: "ParentCommentId",
                principalSchema: "forum",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_PostId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_PostId_UserId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_UserId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentCommentId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "forum",
                table: "Comments");

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "Reactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "forum",
                table: "Comments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "Comments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_UserId_PostType_PostId",
                schema: "forum",
                table: "Reactions",
                columns: new[] { "UserId", "PostType", "PostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostType_PostId",
                schema: "forum",
                table: "Comments",
                columns: new[] { "PostType", "PostId" });
        }
    }
}
