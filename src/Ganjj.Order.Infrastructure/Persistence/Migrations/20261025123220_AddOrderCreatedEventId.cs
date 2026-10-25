using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ganjj.Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCreatedEventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderCreatedEventId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [Orders] SET [OrderCreatedEventId] = NEWID() WHERE [OrderCreatedEventId] IS NULL");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderCreatedEventId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderCreatedEventId",
                table: "Orders",
                column: "OrderCreatedEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderCreatedEventId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderCreatedEventId",
                table: "Orders");
        }
    }
}
