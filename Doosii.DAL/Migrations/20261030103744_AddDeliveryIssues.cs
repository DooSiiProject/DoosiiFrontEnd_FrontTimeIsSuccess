using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryIssues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryIssues", x => x.Id);
                    table.CheckConstraint("CK_DeliveryIssues_Status", "[Status] IN ('FAILED','RETRYING','RETURNING','RETURNED','DELIVERED')");
                    table.ForeignKey(
                        name: "FK_DeliveryIssues_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryIssueEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryIssueId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryIssueEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryIssueEvents_DeliveryIssues_DeliveryIssueId",
                        column: x => x.DeliveryIssueId,
                        principalSchema: "dbo",
                        principalTable: "DeliveryIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryIssueEvents_DeliveryIssueId_Id",
                table: "DeliveryIssueEvents",
                columns: new[] { "DeliveryIssueId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryIssues_OrderId",
                schema: "dbo",
                table: "DeliveryIssues",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryIssueEvents");

            migrationBuilder.DropTable(
                name: "DeliveryIssues",
                schema: "dbo");
        }
    }
}
