using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init35 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChannelEvents",
                type: "TEXT",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256
            );

            migrationBuilder.CreateTable(
                name: "TTSVoices",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SpeakerId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Accent = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TTSVoices", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "UserTTSVoices",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TtsVoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    SetAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTTSVoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTTSVoices_TTSVoices_TtsVoiceId",
                        column: x => x.TtsVoiceId,
                        principalTable: "TTSVoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UserTTSVoices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_UserTTSVoices_TtsVoiceId",
                table: "UserTTSVoices",
                column: "TtsVoiceId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UserTTSVoices_UserId",
                table: "UserTTSVoices",
                column: "UserId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserTTSVoices");

            migrationBuilder.DropTable(name: "TTSVoices");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChannelEvents",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256,
                oldNullable: true
            );
        }
    }
}
