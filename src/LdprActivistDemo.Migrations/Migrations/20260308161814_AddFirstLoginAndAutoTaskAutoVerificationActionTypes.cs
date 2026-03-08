using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstLoginAndAutoTaskAutoVerificationActionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_auto_verification_action_type_allowed",
                table: "tasks");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_auto_verification_action_type_allowed",
                table: "tasks",
                sql: "(\"VerificationType\" = 'manual' AND \"AutoVerificationActionType\" IS NULL) OR (\"VerificationType\" = 'auto' AND \"AutoVerificationActionType\" IN ('invite_friend','first_login','auto'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_auto_verification_action_type_allowed",
                table: "tasks");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_auto_verification_action_type_allowed",
                table: "tasks",
                sql: "(\"VerificationType\" = 'manual' AND \"AutoVerificationActionType\" IS NULL) OR (\"VerificationType\" = 'auto' AND \"AutoVerificationActionType\" IN ('invite_friend'))");
        }
    }
}
