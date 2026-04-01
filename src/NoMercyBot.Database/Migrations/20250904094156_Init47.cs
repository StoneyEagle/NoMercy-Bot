using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init47 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_Provider_EventType",
                table: "EventSubscriptions"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_Provider_EventType_Condition",
                table: "EventSubscriptions",
                columns: new[] { "Provider", "EventType", "Condition" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_Provider_EventType_Condition",
                table: "EventSubscriptions"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_Provider_EventType",
                table: "EventSubscriptions",
                columns: new[] { "Provider", "EventType" },
                unique: true
            );
        }
    }
}
