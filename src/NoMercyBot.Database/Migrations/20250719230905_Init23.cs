using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init23 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_MessageNode_MessageNode_id",
                table: "ChatMessages"
            );

            migrationBuilder.DropTable(name: "ChatMessageFragment");

            migrationBuilder.DropTable(name: "MessageNode");

            migrationBuilder.DropTable(name: "ChatMention");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_MessageNode_id",
                table: "ChatMessages"
            );

            migrationBuilder.DropColumn(name: "MessageNode_id", table: "ChatMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageNode_id",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ChatMention",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserLogin = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMention", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "MessageNode",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Attribs = table.Column<string>(type: "TEXT", nullable: false),
                    Classes = table.Column<string>(type: "TEXT", nullable: true),
                    MessageNode_id = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: true
                    ),
                    Text = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageNode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageNode_MessageNode_MessageNode_id",
                        column: x => x.MessageNode_id,
                        principalTable: "MessageNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ChatMessageFragment",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EmoteId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MentionId = table.Column<string>(type: "TEXT", nullable: true),
                    HtmlPreviewCustomContent = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageFragment", x => x.Id);
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
                name: "IX_ChatMessages_MessageNode_id",
                table: "ChatMessages",
                column: "MessageNode_id"
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

            migrationBuilder.CreateIndex(
                name: "IX_MessageNode_MessageNode_id",
                table: "MessageNode",
                column: "MessageNode_id"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_MessageNode_MessageNode_id",
                table: "ChatMessages",
                column: "MessageNode_id",
                principalTable: "MessageNode",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
