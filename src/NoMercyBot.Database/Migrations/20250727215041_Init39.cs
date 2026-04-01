using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init39 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_UserTtsVoices_UserId", table: "UserTtsVoices");

            migrationBuilder.CreateIndex(
                name: "IX_UserTtsVoices_UserId",
                table: "UserTtsVoices",
                column: "UserId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_UserTtsVoices_UserId", table: "UserTtsVoices");

            migrationBuilder.CreateIndex(
                name: "IX_UserTtsVoices_UserId",
                table: "UserTtsVoices",
                column: "UserId"
            );
        }
    }
}
