using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UnifyCommunityPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This redesign is only authorized for an empty community schema.
            // Locks remain until the migration transaction completes.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [forum].[PassPosts] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[SpotSharingPosts] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[PostImages] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[SpotImages] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[Comments] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[Reactions] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[PostReports] WITH (TABLOCKX, HOLDLOCK))
                    THROW 51007, 'Part 7 requires empty community tables. No data was removed.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropTable(
                name: "PostImages",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "SpotImages",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "PassPosts",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "SpotSharingPosts",
                schema: "forum");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_PostId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_PostType_PostId_UserId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reactions_PostType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions");

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
                name: "IX_PostReports_Status",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PostReports_PostType",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropIndex(
                name: "IX_Comments_CreatedAt",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentCommentId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId",
                schema: "forum",
                table: "Comments");

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
                name: "PostType",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropColumn(
                name: "PostType",
                schema: "forum",
                table: "Comments");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Products_Id_StoreId",
                schema: "store",
                table: "Products",
                columns: new[] { "Id", "StoreId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Comments_Id_PostId",
                schema: "forum",
                table: "Comments",
                columns: new[] { "Id", "PostId" });

            migrationBuilder.CreateTable(
                name: "Posts",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PostDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    VisibilityStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                    table.UniqueConstraint("AK_Posts_Id_Type", x => new { x.Id, x.Type });
                    table.CheckConstraint("CK_Posts_RequestId", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Posts_Text", "LEN(LTRIM(RTRIM([Title]))) > 0 AND LEN(LTRIM(RTRIM([PostDescription]))) > 0");
                    table.CheckConstraint("CK_Posts_Type", "[Type] IN ('PASS','OTHER')");
                    table.CheckConstraint("CK_Posts_Visibility", "[VisibilityStatus] IN ('ACTIVE','HIDDEN')");
                    table.ForeignKey(
                        name: "FK_Posts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PassPostInfos",
                schema: "forum",
                columns: table => new
                {
                    PostId = table.Column<int>(type: "int", nullable: false),
                    PostType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProductDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConditionPercent = table.Column<int>(type: "int", nullable: false),
                    Defects = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StyleTagsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MeasurementsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SaleStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "AVAILABLE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassPostInfos", x => x.PostId);
                    table.CheckConstraint("CK_PassPostInfos_Condition", "[ConditionPercent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_PassPostInfos_Measurements", "[MeasurementsJson] IS NULL OR (ISJSON([MeasurementsJson]) = 1 AND LEFT(LTRIM([MeasurementsJson]), 1) = '[')");
                    table.CheckConstraint("CK_PassPostInfos_Price", "[Price] > 0");
                    table.CheckConstraint("CK_PassPostInfos_SaleStatus", "[SaleStatus] IN ('AVAILABLE','SOLD')");
                    table.CheckConstraint("CK_PassPostInfos_Tags", "ISJSON([StyleTagsJson]) = 1 AND LEFT(LTRIM([StyleTagsJson]), 1) = '['");
                    table.CheckConstraint("CK_PassPostInfos_Text", "LEN(LTRIM(RTRIM([ProductName]))) > 0 AND LEN(LTRIM(RTRIM([Size]))) > 0 AND LEN(LTRIM(RTRIM([Defects]))) > 0");
                    table.CheckConstraint("CK_PassPostInfos_Type", "[PostType] = 'PASS'");
                    table.ForeignKey(
                        name: "FK_PassPostInfos_Posts_PostId_PostType",
                        columns: x => new { x.PostId, x.PostType },
                        principalSchema: "forum",
                        principalTable: "Posts",
                        principalColumns: new[] { "Id", "Type" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostMedia",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    PostType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StoragePublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostMedia", x => x.Id);
                    table.CheckConstraint("CK_PostMedia_PassTags", "[PostType] <> 'PASS' OR ([StoreId] IS NULL AND [ProductId] IS NULL)");
                    table.CheckConstraint("CK_PostMedia_Position", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_PostMedia_ProductStore", "[ProductId] IS NULL OR [StoreId] IS NOT NULL");
                    table.CheckConstraint("CK_PostMedia_Source", "LEN(LTRIM(RTRIM([SourceUrl]))) > 0 AND LEN(LTRIM(RTRIM([StoragePublicId]))) > 0");
                    table.CheckConstraint("CK_PostMedia_Type", "[MediaType] IN ('IMAGE','VIDEO')");
                    table.ForeignKey(
                        name: "FK_PostMedia_Posts_PostId_PostType",
                        columns: x => new { x.PostId, x.PostType },
                        principalSchema: "forum",
                        principalTable: "Posts",
                        principalColumns: new[] { "Id", "Type" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostMedia_Products_ProductId_StoreId",
                        columns: x => new { x.ProductId, x.StoreId },
                        principalSchema: "store",
                        principalTable: "Products",
                        principalColumns: new[] { "Id", "StoreId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostMedia_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "store",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostId_UserId",
                schema: "forum",
                table: "Reactions",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions",
                sql: "[ReactionType] IN ('LIKE','HEART')");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_PostId_ReporterId",
                schema: "forum",
                table: "PostReports",
                columns: new[] { "PostId", "ReporterId" },
                unique: true,
                filter: "[Status] = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_Status_CreatedAt_Id",
                schema: "forum",
                table: "PostReports",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId_PostId",
                schema: "forum",
                table: "Comments",
                columns: new[] { "ParentCommentId", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId_AuthorId_ClientRequestId",
                schema: "forum",
                table: "Comments",
                columns: new[] { "PostId", "AuthorId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId_ParentCommentId_CreatedAt_Id",
                schema: "forum",
                table: "Comments",
                columns: new[] { "PostId", "ParentCommentId", "CreatedAt", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_Content",
                schema: "forum",
                table: "Comments",
                sql: "LEN(LTRIM(RTRIM([Content]))) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PassPostInfos_PostId_PostType",
                schema: "forum",
                table: "PassPostInfos",
                columns: new[] { "PostId", "PostType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostId_DisplayOrder",
                schema: "forum",
                table: "PostMedia",
                columns: new[] { "PostId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostId_PostType",
                schema: "forum",
                table: "PostMedia",
                columns: new[] { "PostId", "PostType" });

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_ProductId_StoreId",
                schema: "forum",
                table: "PostMedia",
                columns: new[] { "ProductId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_StoreId_PostId",
                schema: "forum",
                table: "PostMedia",
                columns: new[] { "StoreId", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId_ClientRequestId",
                schema: "forum",
                table: "Posts",
                columns: new[] { "AuthorId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId_CreatedAt",
                schema: "forum",
                table: "Posts",
                columns: new[] { "AuthorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Type_VisibilityStatus_CreatedAt_Id",
                schema: "forum",
                table: "Posts",
                columns: new[] { "Type", "VisibilityStatus", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_VisibilityStatus_CreatedAt_Id",
                schema: "forum",
                table: "Posts",
                columns: new[] { "VisibilityStatus", "CreatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId_PostId",
                schema: "forum",
                table: "Comments",
                columns: new[] { "ParentCommentId", "PostId" },
                principalSchema: "forum",
                principalTable: "Comments",
                principalColumns: new[] { "Id", "PostId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Posts_PostId",
                schema: "forum",
                table: "Comments",
                column: "PostId",
                principalSchema: "forum",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReports_Posts_PostId",
                schema: "forum",
                table: "PostReports",
                column: "PostId",
                principalSchema: "forum",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reactions_Posts_PostId",
                schema: "forum",
                table: "Reactions",
                column: "PostId",
                principalSchema: "forum",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [forum].[Posts] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[PassPostInfos] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[PostMedia] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[Comments] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[Reactions] WITH (TABLOCKX, HOLDLOCK))
                    OR EXISTS (SELECT 1 FROM [forum].[PostReports] WITH (TABLOCKX, HOLDLOCK))
                    THROW 51008, 'Part 7 rollback requires empty community tables. Back up and migrate data explicitly.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId_PostId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Posts_PostId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_PostReports_Posts_PostId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reactions_Posts_PostId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropTable(
                name: "PassPostInfos",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "PostMedia",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "Posts",
                schema: "forum");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_PostId_UserId",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Products_Id_StoreId",
                schema: "store",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_PostReports_PostId_ReporterId",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropIndex(
                name: "IX_PostReports_Status_CreatedAt_Id",
                schema: "forum",
                table: "PostReports");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Comments_Id_PostId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentCommentId_PostId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId_AuthorId_ClientRequestId",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId_ParentCommentId_CreatedAt_Id",
                schema: "forum",
                table: "Comments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Comments_Content",
                schema: "forum",
                table: "Comments");

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "Reactions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "LEGACY");

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "PostReports",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                schema: "forum",
                table: "Comments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "LEGACY");

            migrationBuilder.CreateTable(
                name: "PassPosts",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    AllowEscrow = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ConditionPercent = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    StyleTags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                name: "SpotSharingPosts",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    TaggedStoreId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    LocationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotSharingPosts", x => x.Id);
                    table.CheckConstraint("CK_SpotSharingPosts_Status", "[Status] IN ('ACTIVE','HIDDEN')");
                    table.ForeignKey(
                        name: "FK_SpotSharingPosts_Stores_TaggedStoreId",
                        column: x => x.TaggedStoreId,
                        principalSchema: "store",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "SpotImages",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpotPostId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpotImages_SpotSharingPosts_SpotPostId",
                        column: x => x.SpotPostId,
                        principalSchema: "forum",
                        principalTable: "SpotSharingPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostId",
                schema: "forum",
                table: "Reactions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_PostType_PostId_UserId",
                schema: "forum",
                table: "Reactions",
                columns: new[] { "PostType", "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions",
                column: "ReactionType");

            // Defer compilation until the restored PostType column exists, also in SQL scripts.
            migrationBuilder.Sql("EXEC(N'ALTER TABLE [forum].[Reactions] ADD CONSTRAINT [CK_Reactions_PostType] CHECK ([PostType] IN (''PASS'',''SPOT'',''LEGACY''))');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reactions_ReactionType",
                schema: "forum",
                table: "Reactions",
                sql: "[ReactionType] IN ('LIKE', 'HEART')");

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
                name: "IX_PostReports_Status",
                schema: "forum",
                table: "PostReports",
                column: "Status");

            migrationBuilder.Sql("EXEC(N'ALTER TABLE [forum].[PostReports] ADD CONSTRAINT [CK_PostReports_PostType] CHECK ([PostType] IN (''PASS'',''SPOT''))');");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_CreatedAt",
                schema: "forum",
                table: "Comments",
                column: "CreatedAt");

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

            migrationBuilder.Sql("EXEC(N'ALTER TABLE [forum].[Comments] ADD CONSTRAINT [CK_Comments_PostType] CHECK ([PostType] IN (''PASS'',''SPOT'',''LEGACY''))');");

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
                name: "IX_SpotImages_SpotPostId_DisplayOrder",
                schema: "forum",
                table: "SpotImages",
                columns: new[] { "SpotPostId", "DisplayOrder" });

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
                name: "IX_SpotSharingPosts_Latitude",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "Latitude");

            migrationBuilder.CreateIndex(
                name: "IX_SpotSharingPosts_Longitude",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "Longitude");

            migrationBuilder.CreateIndex(
                name: "IX_SpotSharingPosts_TaggedStoreId",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "TaggedStoreId");

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
    }
}
