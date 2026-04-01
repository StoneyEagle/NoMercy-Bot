using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class RewardSystemUpdate3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_rewards2", table: "rewards2");

            migrationBuilder.RenameTable(name: "rewards2", newName: "Rewards");

            migrationBuilder.AddPrimaryKey(name: "PK_Rewards", table: "Rewards", column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_Rewards", table: "Rewards");

            migrationBuilder.RenameTable(name: "Rewards", newName: "rewards2");

            migrationBuilder.AddPrimaryKey(name: "PK_rewards2", table: "rewards2", column: "Id");
        }
    }
}
