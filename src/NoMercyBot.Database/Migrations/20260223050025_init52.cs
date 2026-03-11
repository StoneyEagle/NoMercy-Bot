using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class init52 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecorationType",
                table: "ChatEmote");

            migrationBuilder.DropColumn(
                name: "IsDecorated",
                table: "ChatEmote");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecorationType",
                table: "ChatEmote",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDecorated",
                table: "ChatEmote",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
