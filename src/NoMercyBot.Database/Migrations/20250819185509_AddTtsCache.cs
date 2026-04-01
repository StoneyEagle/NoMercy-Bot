using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "TtsUsageRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.CreateTable(
                name: "TtsCacheEntries",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContentHash = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    TextContent = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    VoiceId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<decimal>(
                        type: "TEXT",
                        precision: 18,
                        scale: 6,
                        nullable: false
                    ),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccessCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        rowVersion: true,
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TtsCacheEntries", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TtsCacheEntries_ContentHash",
                table: "TtsCacheEntries",
                column: "ContentHash",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TtsCacheEntries");

            migrationBuilder.DropColumn(name: "Cost", table: "TtsUsageRecords");
        }
    }
}
