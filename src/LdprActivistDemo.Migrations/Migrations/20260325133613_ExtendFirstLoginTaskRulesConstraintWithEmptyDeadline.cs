using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	/// <inheritdoc />
	public partial class ExtendFirstLoginTaskRulesConstraintWithEmptyDeadline : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_tasks_first_login_requires_auto_disposable",
				table: "tasks");

			migrationBuilder.AddCheckConstraint(
				name: "ck_tasks_first_login_requires_auto_disposable",
				table: "tasks",
				sql: "\"AutoVerificationActionType\" IS NULL OR \"AutoVerificationActionType\" <> 'first_login' OR (\"VerificationType\" = 'auto' AND \"ReuseType\" = 'disposable' AND \"DeadlineAt\" IS NULL)");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_tasks_first_login_requires_auto_disposable",
				table: "tasks");

			migrationBuilder.AddCheckConstraint(
				name: "ck_tasks_first_login_requires_auto_disposable",
				table: "tasks",
				sql: "\"AutoVerificationActionType\" IS NULL OR \"AutoVerificationActionType\" <> 'first_login' OR (\"VerificationType\" = 'auto' AND \"ReuseType\" = 'disposable')");
		}
	}
}
