using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init21 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ConditionJson", table: "EventSubscriptions");

            migrationBuilder.DropColumn(name: "MetadataJson", table: "EventSubscriptions");

            migrationBuilder.DropColumn(name: "LabelsJson", table: "ChannelInfo");

            migrationBuilder.DropColumn(name: "TagsJson", table: "ChannelInfo");

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "EventSubscriptions",
                type: "TEXT",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "EventSubscriptions",
                type: "TEXT",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "ContentLabels",
                table: "ChannelInfo",
                type: "TEXT",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "ChannelInfo",
                type: "TEXT",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.CreateTable(
                name: "ChatCheermote",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Bits = table.Column<int>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCheermote", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ChatMention",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserLogin = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMention", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ChatMessageFragment",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CheermoteId = table.Column<string>(type: "TEXT", nullable: true),
                    EmoteId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MentionId = table.Column<string>(type: "TEXT", nullable: true),
                    HtmlPreviewCustomContent = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageFragment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessageFragment_ChatCheermote_CheermoteId",
                        column: x => x.CheermoteId,
                        principalTable: "ChatCheermote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ChatMessageFragment_ChatEmote_EmoteId",
                        column: x => x.EmoteId,
                        principalTable: "ChatEmote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ChatMessageFragment_ChatMention_MentionId",
                        column: x => x.MentionId,
                        principalTable: "ChatMention",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageFragment_CheermoteId",
                table: "ChatMessageFragment",
                column: "CheermoteId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageFragment_EmoteId",
                table: "ChatMessageFragment",
                column: "EmoteId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageFragment_MentionId",
                table: "ChatMessageFragment",
                column: "MentionId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChatMessageFragment");

            migrationBuilder.DropTable(name: "ChatCheermote");

            migrationBuilder.DropTable(name: "ChatEmote");

            migrationBuilder.DropTable(name: "ChatMention");

            migrationBuilder.DropColumn(name: "Condition", table: "EventSubscriptions");

            migrationBuilder.DropColumn(name: "Metadata", table: "EventSubscriptions");

            migrationBuilder.DropColumn(name: "ContentLabels", table: "ChannelInfo");

            migrationBuilder.DropColumn(name: "Tags", table: "ChannelInfo");

            migrationBuilder.AddColumn<string>(
                name: "ConditionJson",
                table: "EventSubscriptions",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "EventSubscriptions",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "LabelsJson",
                table: "ChannelInfo",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "ChannelInfo",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: ""
            );
        }
    }
}
