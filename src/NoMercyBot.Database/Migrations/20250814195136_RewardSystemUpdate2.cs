using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class RewardSystemUpdate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_rewards", table: "rewards");

            migrationBuilder.RenameTable(name: "rewards", newName: "rewards2");

            migrationBuilder.AddPrimaryKey(name: "PK_rewards2", table: "rewards2", column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_rewards2", table: "rewards2");

            migrationBuilder.RenameTable(name: "rewards2", newName: "rewards");

            migrationBuilder.AddPrimaryKey(name: "PK_rewards", table: "rewards", column: "Id");
        }
    }
}
