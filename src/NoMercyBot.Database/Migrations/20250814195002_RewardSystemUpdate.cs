using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class RewardSystemUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "reward_title", table: "rewards");

            migrationBuilder.RenameColumn(name: "response", table: "rewards", newName: "Response");

            migrationBuilder.RenameColumn(
                name: "permission",
                table: "rewards",
                newName: "Permission"
            );

            migrationBuilder.RenameColumn(
                name: "description",
                table: "rewards",
                newName: "Description"
            );

            migrationBuilder.RenameColumn(name: "id", table: "rewards", newName: "Id");

            migrationBuilder.RenameColumn(
                name: "is_enabled",
                table: "rewards",
                newName: "IsEnabled"
            );

            migrationBuilder.RenameColumn(name: "reward_id", table: "rewards", newName: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "Response", table: "rewards", newName: "response");

            migrationBuilder.RenameColumn(
                name: "Permission",
                table: "rewards",
                newName: "permission"
            );

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "rewards",
                newName: "description"
            );

            migrationBuilder.RenameColumn(name: "Id", table: "rewards", newName: "id");

            migrationBuilder.RenameColumn(
                name: "IsEnabled",
                table: "rewards",
                newName: "is_enabled"
            );

            migrationBuilder.RenameColumn(name: "Title", table: "rewards", newName: "reward_id");

            migrationBuilder.AddColumn<string>(
                name: "reward_title",
                table: "rewards",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );
        }
    }
}
