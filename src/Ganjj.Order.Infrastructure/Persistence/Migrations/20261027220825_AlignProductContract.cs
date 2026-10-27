using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ganjj.Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignProductContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Size",
                table: "OrderItems",
                newName: "Color");

            migrationBuilder.AlterColumn<string>(
                name: "ProductId",
                table: "OrderItems",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Color",
                table: "OrderItems",
                newName: "Size");

            migrationBuilder.AlterColumn<long>(
                name: "ProductId",
                table: "OrderItems",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
        }
    }
}
