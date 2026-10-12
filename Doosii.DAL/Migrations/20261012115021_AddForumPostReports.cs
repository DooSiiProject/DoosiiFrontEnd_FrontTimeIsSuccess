using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddForumPostReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostReports_Users_ReportedByUserId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropIndex(
                name: "IX_PostReports_PostType_PostId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.RenameColumn(
                name: "ResolvedAt",
                schema: "forum",
                table: "PostReports",
                newName: "ReviewedAt");

            migrationBuilder.RenameColumn(
                name: "ReportedByUserId",
                schema: "forum",
                table: "PostReports",
                newName: "ReporterId");

            migrationBuilder.RenameIndex(
                name: "IX_PostReports_ReportedByUserId",
                schema: "forum",
                table: "PostReports",
                newName: "IX_PostReports_ReporterId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceUrlsJson",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByAdminId",
                schema: "forum",
                table: "PostReports",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_CreatedAt",
                schema: "forum",
                table: "PostReports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_PostId",
                schema: "forum",
                table: "PostReports",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_PostId_PostType_ReporterId",
                schema: "forum",
                table: "PostReports",
                columns: new[] { "PostId", "PostType", "ReporterId" },
                unique: true,
                filter: "[Status] = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_ReviewedByAdminId",
                schema: "forum",
                table: "PostReports",
                column: "ReviewedByAdminId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PostReports_PostType",
                schema: "forum",
                table: "PostReports",
                sql: "[PostType] IN ('PASS','SPOT')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PostReports_Reason",
                schema: "forum",
                table: "PostReports",
                sql: "[Reason] IN ('SCAM','COUNTERFEIT','OFFENSIVE','PROHIBITED_CONTENT','OTHER')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PostReports_Status",
                schema: "forum",
                table: "PostReports",
                sql: "[Status] IN ('PENDING','REVIEWED','DISMISSED','ACTION_TAKEN')");

            migrationBuilder.AddForeignKey(
                name: "FK_PostReports_Users_ReporterId",
                schema: "forum",
                table: "PostReports",
                column: "ReporterId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReports_Users_ReviewedByAdminId",
                schema: "forum",
                table: "PostReports",
                column: "ReviewedByAdminId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostReports_Users_ReporterId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropForeignKey(
                name: "FK_PostReports_Users_ReviewedByAdminId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropIndex(
                name: "IX_PostReports_CreatedAt",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropIndex(
                name: "IX_PostReports_PostId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropIndex(
                name: "IX_PostReports_PostId_PostType_ReporterId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropIndex(
                name: "IX_PostReports_ReviewedByAdminId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PostReports_PostType",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PostReports_Reason",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PostReports_Status",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropColumn(
                name: "EvidenceUrlsJson",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropColumn(
                name: "ReviewedByAdminId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.RenameColumn(
                name: "ReviewedAt",
                schema: "forum",
                table: "PostReports",
                newName: "ResolvedAt");

            migrationBuilder.RenameColumn(
                name: "ReporterId",
                schema: "forum",
                table: "PostReports",
                newName: "ReportedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_PostReports_ReporterId",
                schema: "forum",
                table: "PostReports",
                newName: "IX_PostReports_ReportedByUserId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "PENDING");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_PostType_PostId",
                schema: "forum",
                table: "PostReports",
                columns: new[] { "PostType", "PostId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PostReports_Users_ReportedByUserId",
                schema: "forum",
                table: "PostReports",
                column: "ReportedByUserId",
                principalSchema: "auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
