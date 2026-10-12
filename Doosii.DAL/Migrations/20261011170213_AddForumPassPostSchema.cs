using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddForumPassPostSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "forum");

            migrationBuilder.CreateTable(
                name: "Comments",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    PostType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PassPosts",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConditionPercent = table.Column<int>(type: "int", nullable: false),
                    AllowEscrow = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    StyleTags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassPosts", x => x.Id);
                    table.CheckConstraint("CK_PassPosts_ConditionPercent", "[ConditionPercent] >= 0 AND [ConditionPercent] <= 100");
                    table.CheckConstraint("CK_PassPosts_Price", "[Price] > 0");
                    table.CheckConstraint("CK_PassPosts_Status", "[Status] IN ('ACTIVE','LOCKED','SOLD','HIDDEN')");
                    table.ForeignKey(
                        name: "FK_PassPosts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostReports",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportedByUserId = table.Column<int>(type: "int", nullable: false),
                    PostType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostReports_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reactions",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PostType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    ReactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reactions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpotSharingPosts",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotSharingPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpotSharingPosts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostImages",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostImages_PassPosts_PostId",
                        column: x => x.PostId,
                        principalSchema: "forum",
                        principalTable: "PassPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                schema: "forum",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_CreatedAt",
                schema: "forum",
                table: "Comments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostType_PostId",
                schema: "forum",
                table: "Comments",
                columns: new[] { "PostType", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_PassPosts_AuthorId",
                schema: "forum",
                table: "PassPosts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PassPosts_CreatedAt",
                schema: "forum",
                table: "PassPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PassPosts_Status",
                schema: "forum",
                table: "PassPosts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PostImages_PostId_DisplayOrder",
                schema: "forum",
                table: "PostImages",
                columns: new[] { "PostId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_PostType_PostId",
                schema: "forum",
                table: "PostReports",
                columns: new[] { "PostType", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_ReportedByUserId",
                schema: "forum",
                table: "PostReports",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_Status",
                schema: "forum",
                table: "PostReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_UserId_PostType_PostId",
                schema: "forum",
                table: "Reactions",
                columns: new[] { "UserId", "PostType", "PostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpotSharingPosts_AuthorId",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_SpotSharingPosts_CreatedAt",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SpotSharingPosts_Status",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comments",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "PostImages",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "PostReports",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "Reactions",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "SpotSharingPosts",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "PassPosts",
                schema: "forum");
        }
    }
}
