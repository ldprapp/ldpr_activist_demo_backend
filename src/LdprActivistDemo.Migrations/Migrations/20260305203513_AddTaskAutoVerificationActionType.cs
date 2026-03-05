using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskAutoVerificationActionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutoVerificationActionType",
                table: "tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_auto_verification_action_type_allowed",
                table: "tasks",
                sql: "(\"VerificationType\" = 'manual' AND \"AutoVerificationActionType\" IS NULL) OR (\"VerificationType\" = 'auto' AND \"AutoVerificationActionType\" IN ('invite_friend'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_auto_verification_action_type_allowed",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "AutoVerificationActionType",
                table: "tasks");
        }
    }
}
