using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AccessToken", table: "Users");

            migrationBuilder.DropColumn(name: "RefreshToken", table: "Users");

            migrationBuilder.DropColumn(name: "TokenExpiry", table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Services",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Services",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiry",
                table: "Services",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AccessToken", table: "Services");

            migrationBuilder.DropColumn(name: "RefreshToken", table: "Services");

            migrationBuilder.DropColumn(name: "TokenExpiry", table: "Services");

            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiry",
                table: "Users",
                type: "TEXT",
                nullable: true
            );
        }
    }
}
