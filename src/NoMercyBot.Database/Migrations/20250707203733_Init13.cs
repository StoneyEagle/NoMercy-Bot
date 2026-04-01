using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CheerBadge", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "EmoteSet", table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "Fragments",
                table: "ChatMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: ""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Fragments", table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "CheerBadge",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "EmoteSet",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true
            );
        }
    }
}
