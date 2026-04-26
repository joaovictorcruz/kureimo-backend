using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kureimo.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddingSetImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Photocards");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Sets",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Sets");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Photocards",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
