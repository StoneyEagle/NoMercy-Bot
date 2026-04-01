using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventSubscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubscriptionId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: true
                    ),
                    CallbackUrl = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "TEXT",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSubscriptions", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Pronouns",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", maxLength: 50, nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Object = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Singular = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pronouns", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_Provider_EventType",
                table: "EventSubscriptions",
                columns: new[] { "Provider", "EventType" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Pronouns_Name",
                table: "Pronouns",
                column: "Name",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EventSubscriptions");

            migrationBuilder.DropTable(name: "Pronouns");
        }
    }
}
