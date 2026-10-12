using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddForumSpotSharingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpotSharingPosts_Status",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "decimal(10,7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "decimal(10,7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaggedStoreId",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpotImages",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpotPostId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpotSharingPosts_Status",
                schema: "forum",
                table: "SpotSharingPosts",
                sql: "[Status] IN ('ACTIVE','HIDDEN')");

            migrationBuilder.CreateIndex(
                name: "IX_SpotImages_SpotPostId_DisplayOrder",
                schema: "forum",
                table: "SpotImages",
                columns: new[] { "SpotPostId", "DisplayOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_SpotSharingPosts_Stores_TaggedStoreId",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "TaggedStoreId",
                principalSchema: "store",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpotSharingPosts_Stores_TaggedStoreId",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropTable(
                name: "SpotImages",
                schema: "forum");

            migrationBuilder.DropIndex(
                name: "IX_SpotSharingPosts_Latitude",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropIndex(
                name: "IX_SpotSharingPosts_Longitude",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropIndex(
                name: "IX_SpotSharingPosts_TaggedStoreId",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SpotSharingPosts_Status",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropColumn(
                name: "LocationName",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.DropColumn(
                name: "TaggedStoreId",
                schema: "forum",
                table: "SpotSharingPosts");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "ACTIVE");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "forum",
                table: "SpotSharingPosts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SpotSharingPosts_Status",
                schema: "forum",
                table: "SpotSharingPosts",
                column: "Status");
        }
    }
}
