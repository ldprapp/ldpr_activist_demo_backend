using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReuseTypeToTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReuseType",
                table: "tasks",
                type: "text",
                nullable: false,
                defaultValue: "disposable");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_reuse_type_allowed",
                table: "tasks",
                sql: "\"ReuseType\" IN ('disposable','reusable')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_reuse_type_allowed",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "ReuseType",
                table: "tasks");
        }
    }
}
