using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init36 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "TtsVoices",
                newName: "Region"
            );

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "TtsVoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Age", table: "TtsVoices");

            migrationBuilder.RenameColumn(
                name: "Region",
                table: "TTSVoices",
                newName: "Description"
            );
        }
    }
}
