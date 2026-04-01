using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init41 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Users_ChannelId",
                table: "ChatMessages"
            );

            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "ChatMessages",
                newName: "BroadcasterId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_ChannelId",
                table: "ChatMessages",
                newName: "IX_ChatMessages_BroadcasterId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users_BroadcasterId",
                table: "ChatMessages",
                column: "BroadcasterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Users_BroadcasterId",
                table: "ChatMessages"
            );

            migrationBuilder.RenameColumn(
                name: "BroadcasterId",
                table: "ChatMessages",
                newName: "ChannelId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_BroadcasterId",
                table: "ChatMessages",
                newName: "IX_ChatMessages_ChannelId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users_ChannelId",
                table: "ChatMessages",
                column: "ChannelId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
