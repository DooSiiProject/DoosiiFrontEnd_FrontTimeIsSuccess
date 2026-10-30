using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ReturnRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    BuyerId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SellerNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReturnCarrier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReturnTrackingCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RefundEvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ShippingFeePayer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeadlineAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequests", x => x.Id);
                    table.CheckConstraint("CK_ReturnRequests_FeePayer", "[ShippingFeePayer] IS NULL OR [ShippingFeePayer] IN ('BUYER','SELLER')");
                    table.CheckConstraint("CK_ReturnRequests_RefundAmount", "[RefundAmount] IS NULL OR [RefundAmount] >= 0");
                    table.CheckConstraint("CK_ReturnRequests_Status", "[Status] IN ('REQUESTED','APPROVED','REJECTED','RETURN_SHIPPED','RETURN_RECEIVED','REFUND_PENDING','REFUND_COMPLETED','ADMIN_REVIEW')");
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequestEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnRequestEvents_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalSchema: "dbo",
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestEvents_ReturnRequestId_Id",
                table: "ReturnRequestEvents",
                columns: new[] { "ReturnRequestId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_OrderId",
                schema: "dbo",
                table: "ReturnRequests",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReturnRequestEvents");

            migrationBuilder.DropTable(
                name: "ReturnRequests",
                schema: "dbo");
        }
    }
}
