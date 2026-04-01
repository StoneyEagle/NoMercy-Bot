using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMercyBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class Init16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Bits", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "BitsInDollars", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "BotUsername", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "CustomRewardId", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsBroadcaster", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsFirstMessage", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsMe", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsModerator", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsPartner", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsReturningChatter", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsSkippingSubMode", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsStaff", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsSubscriber", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsTurbo", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "IsVip", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "Noisy", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "RewardId", table: "ChatMessages");

            migrationBuilder.DropColumn(name: "SubscribedMonthCount", table: "ChatMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bits",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<double>(
                name: "BitsInDollars",
                table: "ChatMessages",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<string>(
                name: "BotUsername",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "CustomRewardId",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsBroadcaster",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstMessage",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsMe",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsModerator",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsPartner",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsReturningChatter",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsSkippingSubMode",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsStaff",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsSubscriber",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsTurbo",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsVip",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "Noisy",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "RewardId",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "SubscribedMonthCount",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );
        }
    }
}
