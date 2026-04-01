using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessageFragment_ChatCheermote_CheermoteId",
                table: "ChatMessageFragment"
            );

            migrationBuilder.DropTable(name: "ChatCheermote");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessageFragment_CheermoteId",
                table: "ChatMessageFragment"
            );

            migrationBuilder.DropColumn(name: "CheermoteId", table: "ChatMessageFragment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheermoteId",
                table: "ChatMessageFragment",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ChatCheermote",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Bits = table.Column<int>(type: "INTEGER", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCheermote", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageFragment_CheermoteId",
                table: "ChatMessageFragment",
                column: "CheermoteId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessageFragment_ChatCheermote_CheermoteId",
                table: "ChatMessageFragment",
                column: "CheermoteId",
                principalTable: "ChatCheermote",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
