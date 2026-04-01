using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init31 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Color", table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "SuccessfulReply",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SuccessfulReply", table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true
            );
        }
    }
}
