using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init19 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Services",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Services",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "MessageNode_id",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "MessageNode",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Classes = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Attribs = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    MessageNode_id = table.Column<string>(
                        type: "TEXT",
                        maxLength: 256,
                        nullable: true
                    ),
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

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_MessageNode_id",
                table: "ChatMessages",
                column: "MessageNode_id"
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_MessageNode_MessageNode_id",
                table: "ChatMessages"
            );

            migrationBuilder.DropTable(name: "MessageNode");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_MessageNode_id",
                table: "ChatMessages"
            );

            migrationBuilder.DropColumn(name: "UserId", table: "Services");

            migrationBuilder.DropColumn(name: "UserName", table: "Services");

            migrationBuilder.DropColumn(name: "MessageNode_id", table: "ChatMessages");
        }
    }
}
