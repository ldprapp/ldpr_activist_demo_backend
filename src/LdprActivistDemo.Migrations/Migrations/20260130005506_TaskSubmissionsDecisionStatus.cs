using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TaskSubmissionsDecisionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_submissions_users_ConfirmedByAdminId",
                table: "task_submissions");

            migrationBuilder.RenameColumn(
                name: "ConfirmedByAdminId",
                table: "task_submissions",
                newName: "DecidedByAdminId");

            migrationBuilder.RenameColumn(
                name: "ConfirmedAt",
                table: "task_submissions",
                newName: "DecidedAt");

            migrationBuilder.RenameIndex(
                name: "IX_task_submissions_ConfirmedByAdminId",
                table: "task_submissions",
                newName: "IX_task_submissions_DecidedByAdminId");

            migrationBuilder.AddColumn<string>(
                name: "DecisionStatus",
                table: "task_submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_task_submissions_decision_status_allowed",
                table: "task_submissions",
                sql: "\"DecisionStatus\" IS NULL OR \"DecisionStatus\" IN ('approve','rejected')");

            migrationBuilder.AddForeignKey(
                name: "FK_task_submissions_users_DecidedByAdminId",
                table: "task_submissions",
                column: "DecidedByAdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_submissions_users_DecidedByAdminId",
                table: "task_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_task_submissions_decision_status_allowed",
                table: "task_submissions");

            migrationBuilder.DropColumn(
                name: "DecisionStatus",
                table: "task_submissions");

            migrationBuilder.RenameColumn(
                name: "DecidedByAdminId",
                table: "task_submissions",
                newName: "ConfirmedByAdminId");

            migrationBuilder.RenameColumn(
                name: "DecidedAt",
                table: "task_submissions",
                newName: "ConfirmedAt");

            migrationBuilder.RenameIndex(
                name: "IX_task_submissions_DecidedByAdminId",
                table: "task_submissions",
                newName: "IX_task_submissions_ConfirmedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_task_submissions_users_ConfirmedByAdminId",
                table: "task_submissions",
                column: "ConfirmedByAdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
