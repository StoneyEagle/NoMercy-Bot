using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsProviderSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TtsProviders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_TtsProviders", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "TtsUsageRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProviderId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    CharactersUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    BillingPeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BillingPeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_TtsUsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TtsUsageRecords_TtsProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "TtsProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TtsUsageRecords_ProviderId",
                table: "TtsUsageRecords",
                column: "ProviderId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TtsUsageRecords");

            migrationBuilder.DropTable(name: "TtsProviders");
        }
    }
}
