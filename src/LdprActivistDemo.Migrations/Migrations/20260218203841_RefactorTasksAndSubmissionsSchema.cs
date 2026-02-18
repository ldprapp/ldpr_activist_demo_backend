using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	/// <inheritdoc />
	public partial class RefactorTasksAndSubmissionsSchema : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"
ALTER TABLE task_submissions
	DROP CONSTRAINT IF EXISTS ck_task_submissions_decision_status_allowed;

ALTER TABLE tasks
	DROP CONSTRAINT IF EXISTS ck_tasks_status_allowed;
");

			migrationBuilder.Sql(@"
ALTER TABLE tasks
	ALTER COLUMN ""Status"" TYPE text
	USING CASE ""Status""
		WHEN 0 THEN 'open'
		WHEN 1 THEN 'closed'
		ELSE 'open'
	END;
");

			migrationBuilder.Sql(@"
ALTER TABLE task_submissions
	ALTER COLUMN ""DecisionStatus"" SET DEFAULT 'in_progress';
");

			migrationBuilder.AddCheckConstraint(
				name: "ck_tasks_status_allowed",
				table: "tasks",
				sql: @"""Status"" IN ('open','closed')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_task_submissions_decision_status_allowed",
				table: "task_submissions",
				sql: @"""DecisionStatus"" IS NULL OR ""DecisionStatus"" IN ('in_progress','approve','rejected')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"
ALTER TABLE tasks
	DROP CONSTRAINT IF EXISTS ck_tasks_status_allowed;

ALTER TABLE task_submissions
	DROP CONSTRAINT IF EXISTS ck_task_submissions_decision_status_allowed;
");

			migrationBuilder.Sql(@"
UPDATE task_submissions
SET ""DecisionStatus"" = NULL
WHERE ""DecisionStatus"" = 'in_progress';
");

			migrationBuilder.Sql(@"
ALTER TABLE task_submissions
	ALTER COLUMN ""DecisionStatus"" DROP DEFAULT;
");

			migrationBuilder.Sql(@"
ALTER TABLE tasks
	ALTER COLUMN ""Status"" TYPE integer
	USING CASE ""Status""
		WHEN 'open' THEN 0
		WHEN 'closed' THEN 1
		ELSE 0
	END;
");

			migrationBuilder.AddCheckConstraint(
				name: "ck_task_submissions_decision_status_allowed",
				table: "task_submissions",
				sql: @"""DecisionStatus"" IS NULL OR ""DecisionStatus"" IN ('approve','rejected')");
		}
	}
}
