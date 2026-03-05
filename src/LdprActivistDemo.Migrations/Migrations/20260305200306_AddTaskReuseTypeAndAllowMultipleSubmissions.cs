using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskReuseTypeAndAllowMultipleSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_submissions_TaskId_UserId",
                table: "task_submissions");

            migrationBuilder.CreateIndex(
                name: "IX_task_submissions_TaskId_UserId",
                table: "task_submissions",
                columns: new[] { "TaskId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_submissions_TaskId_UserId",
                table: "task_submissions");

            migrationBuilder.CreateIndex(
                name: "IX_task_submissions_TaskId_UserId",
                table: "task_submissions",
                columns: new[] { "TaskId", "UserId" },
                unique: true);
        }
    }
}
