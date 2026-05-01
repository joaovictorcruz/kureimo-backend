using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kureimo.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderToPhotocard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Photocards",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "Photocards");
        }
    }
}
