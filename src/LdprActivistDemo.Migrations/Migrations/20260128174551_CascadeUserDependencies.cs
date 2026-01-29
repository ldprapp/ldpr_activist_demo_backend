using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CascadeUserDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_submissions_users_ConfirmedByAdminId",
                table: "task_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_task_submissions_users_UserId",
                table: "task_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_task_trusted_admins_users_AdminUserId",
                table: "task_trusted_admins");

            migrationBuilder.AddForeignKey(
                name: "FK_task_submissions_users_ConfirmedByAdminId",
                table: "task_submissions",
                column: "ConfirmedByAdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_task_submissions_users_UserId",
                table: "task_submissions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_task_trusted_admins_users_AdminUserId",
                table: "task_trusted_admins",
                column: "AdminUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_submissions_users_ConfirmedByAdminId",
                table: "task_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_task_submissions_users_UserId",
                table: "task_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_task_trusted_admins_users_AdminUserId",
                table: "task_trusted_admins");

            migrationBuilder.AddForeignKey(
                name: "FK_task_submissions_users_ConfirmedByAdminId",
                table: "task_submissions",
                column: "ConfirmedByAdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_task_submissions_users_UserId",
                table: "task_submissions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_task_trusted_admins_users_AdminUserId",
                table: "task_trusted_admins",
                column: "AdminUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
