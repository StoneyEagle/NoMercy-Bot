using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChannelInfo_Channels_Id",
                table: "ChannelInfo"
            );

            migrationBuilder.DropForeignKey(name: "FK_Channels_Users_Id", table: "Channels");

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_ChannelInfo_Id",
                table: "Channels",
                column: "Id",
                principalTable: "ChannelInfo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Channels_Id",
                table: "Users",
                column: "Id",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Channels_ChannelInfo_Id", table: "Channels");

            migrationBuilder.DropForeignKey(name: "FK_Users_Channels_Id", table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelInfo_Channels_Id",
                table: "ChannelInfo",
                column: "Id",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
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
    }
}
