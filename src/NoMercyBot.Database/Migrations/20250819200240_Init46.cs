using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init46 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigurationJson",
                table: "TtsProviders",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerCharacter",
                table: "TtsProviders",
                type: "TEXT",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<int>(
                name: "MaxCharactersPerRequest",
                table: "TtsProviders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "MonthlyCharacterLimit",
                table: "TtsProviders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ConfigurationJson", table: "TtsProviders");

            migrationBuilder.DropColumn(name: "CostPerCharacter", table: "TtsProviders");

            migrationBuilder.DropColumn(name: "MaxCharactersPerRequest", table: "TtsProviders");

            migrationBuilder.DropColumn(name: "MonthlyCharacterLimit", table: "TtsProviders");
        }
    }
}
