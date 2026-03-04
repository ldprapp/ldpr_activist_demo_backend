using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskVerificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationType",
                table: "tasks",
                type: "text",
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_verification_type_allowed",
                table: "tasks",
                sql: "\"VerificationType\" IN ('auto','manual')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_verification_type_allowed",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "VerificationType",
                table: "tasks");
        }
    }
}
