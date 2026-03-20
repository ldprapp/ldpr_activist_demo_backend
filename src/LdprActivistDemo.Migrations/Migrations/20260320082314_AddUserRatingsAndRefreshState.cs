using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRatingsAndRefreshState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_ratings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallRank = table.Column<int>(type: "integer", nullable: true),
                    RegionRank = table.Column<int>(type: "integer", nullable: true),
                    SettlementRank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_ratings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_user_ratings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_ratings_refresh_state",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "text", nullable: false),
                    LastCompletedLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_ratings_refresh_state", x => x.JobName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_ratings");

            migrationBuilder.DropTable(
                name: "user_ratings_refresh_state");
        }
    }
}
