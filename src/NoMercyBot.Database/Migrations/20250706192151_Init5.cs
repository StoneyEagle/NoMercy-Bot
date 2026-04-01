using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Channels_Users_UserId", table: "Channels");

            migrationBuilder.DropIndex(name: "IX_Channels_UserId", table: "Channels");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Channels",
                newName: "ShoutoutTemplate"
            );

            migrationBuilder.AddColumn<string>(
                name: "PronounData",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "Channels",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "LastShoutout",
                table: "Channels",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Channels",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<int>(
                name: "ShoutoutInterval",
                table: "Channels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateTable(
                name: "ChannelInfo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Delay = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    LabelsJson = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    IsBrandedContent = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelInfo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelInfo_Channels_Id",
                        column: x => x.Id,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ChannelModerator",
                columns: table => new
                {
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelModerator", x => new { x.ChannelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ChannelModerator_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ChannelModerator_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Shoutout",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MessageTemplate = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                    LastShoutout = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ShoutedUserId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shoutout", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shoutout_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Shoutout_Users_ShoutedUserId",
                        column: x => x.ShoutedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChannelModerator_ChannelId",
                table: "ChannelModerator",
                column: "ChannelId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChannelModerator_UserId",
                table: "ChannelModerator",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Shoutout_ChannelId_ShoutedUserId",
                table: "Shoutout",
                columns: new[] { "ChannelId", "ShoutedUserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Shoutout_ShoutedUserId",
                table: "Shoutout",
                column: "ShoutedUserId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Users_Id",
                table: "Channels",
                column: "Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Channels_Users_Id", table: "Channels");

            migrationBuilder.DropTable(name: "ChannelInfo");

            migrationBuilder.DropTable(name: "ChannelModerator");

            migrationBuilder.DropTable(name: "Shoutout");

            migrationBuilder.DropColumn(name: "PronounData", table: "Users");

            migrationBuilder.DropColumn(name: "Enabled", table: "Channels");

            migrationBuilder.DropColumn(name: "LastShoutout", table: "Channels");

            migrationBuilder.DropColumn(name: "Name", table: "Channels");

            migrationBuilder.DropColumn(name: "ShoutoutInterval", table: "Channels");

            migrationBuilder.RenameColumn(
                name: "ShoutoutTemplate",
                table: "Channels",
                newName: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Channels_UserId",
                table: "Channels",
                column: "UserId",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Users_UserId",
                table: "Channels",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
