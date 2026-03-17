using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinatorAndTaskToUserPointsTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoordinatorUserId",
                table: "user_points_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskId",
                table: "user_points_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_points_transactions_CoordinatorUserId",
                table: "user_points_transactions",
                column: "CoordinatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_points_transactions_TaskId",
                table: "user_points_transactions",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_points_transactions_tasks_TaskId",
                table: "user_points_transactions",
                column: "TaskId",
                principalTable: "tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_points_transactions_users_CoordinatorUserId",
                table: "user_points_transactions",
                column: "CoordinatorUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_points_transactions_tasks_TaskId",
                table: "user_points_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_user_points_transactions_users_CoordinatorUserId",
                table: "user_points_transactions");

            migrationBuilder.DropIndex(
                name: "IX_user_points_transactions_CoordinatorUserId",
                table: "user_points_transactions");

            migrationBuilder.DropIndex(
                name: "IX_user_points_transactions_TaskId",
                table: "user_points_transactions");

            migrationBuilder.DropColumn(
                name: "CoordinatorUserId",
                table: "user_points_transactions");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "user_points_transactions");
        }
    }
}
