using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileAndMerchantProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Users",
                newSchema: "auth");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                schema: "auth",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                schema: "auth",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MerchantProfiles",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    KycStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    LicenseImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FrontFacadeUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IdCardFrontUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IdCardBackUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantProfiles_UserId",
                schema: "auth",
                table: "MerchantProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantProfiles",
                schema: "auth");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                schema: "auth",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "auth",
                newName: "Users");
        }
    }
}
