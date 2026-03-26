using Microsoft.EntityFrameworkCore.Migrations;

namespace LdprActivistDemo.Migrations.Migrations;

public partial class AddFirstLoginTaskRulesConstraint : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			UPDATE tasks
			SET
				"VerificationType" = 'auto',
				"ReuseType" = 'disposable'
			WHERE
				"AutoVerificationActionType" = 'first_login'
				AND (
					"VerificationType" <> 'auto'
					OR "ReuseType" <> 'disposable'
				);
			""");

		migrationBuilder.AddCheckConstraint(
			name: "ck_tasks_first_login_requires_auto_disposable",
			table: "tasks",
			sql:
				"\"AutoVerificationActionType\" IS NULL OR \"AutoVerificationActionType\" <> 'first_login' OR (\"VerificationType\" = 'auto' AND \"ReuseType\" = 'disposable')");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropCheckConstraint(
			name: "ck_tasks_first_login_requires_auto_disposable",
			table: "tasks");
	}
}