using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init28 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Badges",
                table: "ChatMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "BitsAmount",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsCheer",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BitsAmount", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsCheer", table: "ChatMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Badges",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT"
            );
        }
    }
}
