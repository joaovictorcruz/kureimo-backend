using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kureimo.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelledAtAndStyleToSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "Sets",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FontColor",
                table: "Sets",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "FontColor",
                table: "Sets");
        }
    }
}
