using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernamePronunciation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Shoutout");

            migrationBuilder.AddColumn<string>(
                name: "UsernamePronunciation",
                table: "Channels",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsernamePronunciation",
                table: "Channels");

            migrationBuilder.CreateTable(
                name: "Shoutout",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ShoutedUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastShoutout = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MessageTemplate = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shoutout", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shoutout_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Shoutout_Users_ShoutedUserId",
                        column: x => x.ShoutedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shoutout_ChannelId_ShoutedUserId",
                table: "Shoutout",
                columns: new[] { "ChannelId", "ShoutedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shoutout_ShoutedUserId",
                table: "Shoutout",
                column: "ShoutedUserId");
        }
    }
}
