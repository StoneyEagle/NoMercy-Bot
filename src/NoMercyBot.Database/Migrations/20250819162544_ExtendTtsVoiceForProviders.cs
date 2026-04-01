using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class ExtendTtsVoiceForProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TtsVoices",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "TtsVoices",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TtsVoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "TtsVoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "TtsVoices",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "en-US"
            );

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "TtsVoices",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "Legacy"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TtsVoices",
                type: "TEXT",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatedAt", table: "TtsVoices");

            migrationBuilder.DropColumn(name: "DisplayName", table: "TtsVoices");

            migrationBuilder.DropColumn(name: "IsActive", table: "TtsVoices");

            migrationBuilder.DropColumn(name: "IsDefault", table: "TtsVoices");

            migrationBuilder.DropColumn(name: "Locale", table: "TtsVoices");

            migrationBuilder.DropColumn(name: "Provider", table: "TtsVoices");

            migrationBuilder.DropColumn(name: "UpdatedAt", table: "TtsVoices");
        }
    }
}
