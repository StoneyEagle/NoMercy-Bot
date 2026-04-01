using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init30 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsLive", table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "StreamId",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsLive",
                table: "ChannelInfo",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateTable(
                name: "Streams",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Delay = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    ContentLabels = table.Column<string>(type: "TEXT", nullable: false),
                    IsBrandedContent = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_Streams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Streams_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_StreamId",
                table: "ChatMessages",
                column: "StreamId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Streams_ChannelId",
                table: "Streams",
                column: "ChannelId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Streams_StreamId",
                table: "ChatMessages",
                column: "StreamId",
                principalTable: "Streams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Streams_StreamId",
                table: "ChatMessages"
            );

            migrationBuilder.DropTable(name: "Streams");

            migrationBuilder.DropIndex(name: "IX_ChatMessages_StreamId", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "StreamId", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsLive", table: "ChannelInfo");

            migrationBuilder.AddColumn<bool>(
                name: "IsLive",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }
    }
}
