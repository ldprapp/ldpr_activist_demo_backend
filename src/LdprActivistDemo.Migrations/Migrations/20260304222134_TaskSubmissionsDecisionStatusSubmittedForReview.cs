using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TaskSubmissionsDecisionStatusSubmittedForReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_task_submissions_decision_status_allowed",
                table: "task_submissions");

            migrationBuilder.AlterColumn<string>(
                name: "DecisionStatus",
                table: "task_submissions",
                type: "text",
                nullable: false,
                defaultValue: "in_progress",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "in_progress");

            migrationBuilder.AddCheckConstraint(
                name: "ck_task_submissions_decision_status_allowed",
                table: "task_submissions",
                sql: "\"DecisionStatus\" IN ('in_progress','submitted_for_review','approve','rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_task_submissions_decision_status_allowed",
                table: "task_submissions");

            migrationBuilder.AlterColumn<string>(
                name: "DecisionStatus",
                table: "task_submissions",
                type: "text",
                nullable: true,
                defaultValue: "in_progress",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "in_progress");

            migrationBuilder.AddCheckConstraint(
                name: "ck_task_submissions_decision_status_allowed",
                table: "task_submissions",
                sql: "\"DecisionStatus\" IS NULL OR \"DecisionStatus\" IN ('in_progress','approve','rejected')");
        }
    }
}
