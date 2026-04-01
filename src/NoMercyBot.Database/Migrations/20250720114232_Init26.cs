using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Shoutout",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Shoutout",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatPresences",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChatPresences",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Channels",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Channels",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ChannelModerator",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChannelModerator",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ChannelInfo",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChannelInfo",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatedAt", table: "Users");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Users");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "Shoutout");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Shoutout");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "ChatPresences");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "ChatPresences");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "Channels");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Channels");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "ChannelModerator");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "ChannelModerator");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "ChannelInfo");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "ChannelInfo");
        }
    }
}
