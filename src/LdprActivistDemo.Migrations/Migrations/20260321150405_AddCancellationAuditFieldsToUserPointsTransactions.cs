using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationAuditFieldsToUserPointsTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAtUtc",
                table: "user_points_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByAdminUserId",
                table: "user_points_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_points_transactions_CancelledByAdminUserId",
                table: "user_points_transactions",
                column: "CancelledByAdminUserId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_points_transactions_cancel_state",
                table: "user_points_transactions",
                sql: "(\"IsCancelled\" = FALSE AND \"CancellationComment\" = '' AND \"CancelledAtUtc\" IS NULL AND \"CancelledByAdminUserId\" IS NULL) OR (\"IsCancelled\" = TRUE AND length(btrim(\"CancellationComment\")) > 0 AND \"CancelledAtUtc\" IS NOT NULL AND \"CancelledByAdminUserId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_user_points_transactions_users_CancelledByAdminUserId",
                table: "user_points_transactions",
                column: "CancelledByAdminUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_points_transactions_users_CancelledByAdminUserId",
                table: "user_points_transactions");

            migrationBuilder.DropIndex(
                name: "IX_user_points_transactions_CancelledByAdminUserId",
                table: "user_points_transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_points_transactions_cancel_state",
                table: "user_points_transactions");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "user_points_transactions");

            migrationBuilder.DropColumn(
                name: "CancelledByAdminUserId",
                table: "user_points_transactions");
        }
    }
}
