using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservedPickupReservationId",
                schema: "store",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PickupReservations",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuyerId = table.Column<int>(type: "int", nullable: false),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BuyerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductTitleSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProductVersionSnapshot = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false),
                    StoreNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StoreAddressSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StoreLatitudeSnapshot = table.Column<double>(type: "float", nullable: true),
                    StoreLongitudeSnapshot = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedById = table.Column<int>(type: "int", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PickupCodeHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    ProtectedPickupCode = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FailedCodeAttempts = table.Column<int>(type: "int", nullable: false),
                    CodeLockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupReservations", x => x.Id);
                    table.UniqueConstraint("AK_PickupReservations_Id_ProductId", x => new { x.Id, x.ProductId });
                    table.CheckConstraint("CK_Pickup_Acceptance", "([AcceptedAt] IS NULL AND [ExpiresAt] IS NULL AND [StoreNameSnapshot] IS NULL AND [StoreAddressSnapshot] IS NULL AND [PickupCodeHash] IS NULL AND [ProtectedPickupCode] IS NULL AND [Status] IN ('PENDING','REJECTED','CANCELLED','EXPIRED')) OR ([AcceptedAt] IS NOT NULL AND [AcceptedAt] >= [CreatedAt] AND [AcceptedAt] < [ResponseDeadline] AND [ExpiresAt] IS NOT NULL AND [ExpiresAt] = DATEADD(minute,30,[ScheduledAt]) AND [StoreNameSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreNameSnapshot]))) > 0 AND [StoreAddressSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreAddressSnapshot]))) > 0 AND [PickupCodeHash] IS NOT NULL AND DATALENGTH([PickupCodeHash]) = 32 AND [ProtectedPickupCode] IS NOT NULL AND LEN([ProtectedPickupCode]) > 0 AND [Status] IN ('ACCEPTED','CANCELLED','EXPIRED','COMPLETED'))");
                    table.CheckConstraint("CK_Pickup_BuyerSeller", "[BuyerId] <> [SellerId]");
                    table.CheckConstraint("CK_Pickup_Closure", "([Status] IN ('PENDING','ACCEPTED') AND [ClosedAt] IS NULL AND [ClosedById] IS NULL AND [CloseReason] IS NULL) OR ([Status] IN ('REJECTED','CANCELLED','EXPIRED','COMPLETED') AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [CreatedAt] AND ([AcceptedAt] IS NULL OR [ClosedAt] >= [AcceptedAt]) AND [CloseReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CloseReason]))) > 0 AND (([Status] = 'EXPIRED' AND [ClosedById] IS NULL AND (([AcceptedAt] IS NULL AND [ClosedAt] >= [ResponseDeadline]) OR ([AcceptedAt] IS NOT NULL AND [ClosedAt] >= [ExpiresAt]))) OR ([Status] = 'COMPLETED' AND [ClosedById] IS NOT NULL AND [ClosedById] = [SellerId] AND [ClosedAt] < [ExpiresAt]) OR ([Status] = 'REJECTED' AND [ClosedById] IS NOT NULL AND [ClosedById] = [SellerId]) OR ([Status] = 'CANCELLED' AND [ClosedById] IS NOT NULL AND [ClosedById] IN ([BuyerId],[SellerId]))))");
                    table.CheckConstraint("CK_Pickup_CodeAttempts", "[FailedCodeAttempts] BETWEEN 0 AND 5 AND ([CodeLockedUntil] IS NULL OR ([AcceptedAt] IS NOT NULL AND [CodeLockedUntil] >= [AcceptedAt]))");
                    table.CheckConstraint("CK_Pickup_Coordinates", "([StoreLatitudeSnapshot] IS NULL AND [StoreLongitudeSnapshot] IS NULL) OR ([StoreLatitudeSnapshot] IS NOT NULL AND [StoreLongitudeSnapshot] IS NOT NULL AND [StoreLatitudeSnapshot] BETWEEN -90 AND 90 AND [StoreLongitudeSnapshot] BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_Pickup_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64 AND [RequestHash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%' AND DATALENGTH([ProductVersionSnapshot]) = 8");
                    table.CheckConstraint("CK_Pickup_Schedule", "[ScheduledAt] >= DATEADD(minute,30,[CreatedAt]) AND [ScheduledAt] <= DATEADD(hour,24,[CreatedAt]) AND [ResponseDeadline] > [CreatedAt] AND [ResponseDeadline] <= [ScheduledAt] AND [ResponseDeadline] <= DATEADD(hour,2,[CreatedAt])");
                    table.CheckConstraint("CK_Pickup_Status", "[Status] IN ('PENDING','ACCEPTED','REJECTED','CANCELLED','EXPIRED','COMPLETED')");
                    table.CheckConstraint("CK_Pickup_TextPrice", "[PriceSnapshot] > 0 AND LEN(LTRIM(RTRIM([ContactName]))) > 0 AND LEN(LTRIM(RTRIM([ContactPhone]))) > 0 AND LEN(LTRIM(RTRIM([ProductTitleSnapshot]))) > 0");
                    table.ForeignKey(
                        name: "FK_PickupReservations_Products_ProductId_StoreId",
                        columns: x => new { x.ProductId, x.StoreId },
                        principalSchema: "store",
                        principalTable: "Products",
                        principalColumns: new[] { "Id", "StoreId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickupReservations_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "store",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickupReservations_Users_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickupReservations_Users_ClosedById",
                        column: x => x.ClosedById,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickupReservations_Users_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ReservedPickupReservationId_Id",
                schema: "store",
                table: "Products",
                columns: new[] { "ReservedPickupReservationId", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ReservationOwner",
                schema: "store",
                table: "Products",
                sql: "[ReservedPickupReservationId] IS NULL OR ([ReservedOrderId] IS NULL AND [Status] = 'LOCKED')");

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_BuyerId_ClientRequestId",
                schema: "store",
                table: "PickupReservations",
                columns: new[] { "BuyerId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_BuyerId_ProductId",
                schema: "store",
                table: "PickupReservations",
                columns: new[] { "BuyerId", "ProductId" },
                unique: true,
                filter: "[Status] IN ('PENDING','ACCEPTED')");

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_BuyerId_Status_CreatedAt_Id",
                schema: "store",
                table: "PickupReservations",
                columns: new[] { "BuyerId", "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_ClosedById",
                schema: "store",
                table: "PickupReservations",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_ExpiresAt",
                schema: "store",
                table: "PickupReservations",
                column: "ExpiresAt",
                filter: "[Status] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_ProductId",
                schema: "store",
                table: "PickupReservations",
                column: "ProductId",
                unique: true,
                filter: "[Status] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_ProductId_StoreId",
                schema: "store",
                table: "PickupReservations",
                columns: new[] { "ProductId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_ResponseDeadline",
                schema: "store",
                table: "PickupReservations",
                column: "ResponseDeadline",
                filter: "[Status] = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_SellerId",
                schema: "store",
                table: "PickupReservations",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_PickupReservations_StoreId_Status_CreatedAt_Id",
                schema: "store",
                table: "PickupReservations",
                columns: new[] { "StoreId", "Status", "CreatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_PickupReservations_ReservedPickupReservationId_Id",
                schema: "store",
                table: "Products",
                columns: new[] { "ReservedPickupReservationId", "Id" },
                principalSchema: "store",
                principalTable: "PickupReservations",
                principalColumns: new[] { "Id", "ProductId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [store].[PickupReservations]) THROW 51009, 'Cannot roll back pickup schema with reservation history.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_Products_PickupReservations_ReservedPickupReservationId_Id",
                schema: "store",
                table: "Products");

            migrationBuilder.DropTable(
                name: "PickupReservations",
                schema: "store");

            migrationBuilder.DropIndex(
                name: "IX_Products_ReservedPickupReservationId_Id",
                schema: "store",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ReservationOwner",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReservedPickupReservationId",
                schema: "store",
                table: "Products");

        }
    }
}
