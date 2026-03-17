#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace LdprActivistDemo.Migrations.Migrations
{
	public partial class RenameTaskAdminNamingToCoordinator : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_task_submissions_users_DecidedByAdminId",
				table: "task_submissions");

			migrationBuilder.DropForeignKey(
				name: "FK_task_trusted_admins_tasks_TaskId",
				table: "task_trusted_admins");

			migrationBuilder.DropForeignKey(
				name: "FK_task_trusted_admins_users_AdminUserId",
				table: "task_trusted_admins");

			migrationBuilder.DropPrimaryKey(
				name: "PK_task_trusted_admins",
				table: "task_trusted_admins");

			migrationBuilder.RenameTable(
				name: "task_trusted_admins",
				newName: "task_trusted_coordinators");

			migrationBuilder.RenameColumn(
				name: "DecidedByAdminId",
				table: "task_submissions",
				newName: "DecidedByCoordinatorId");

			migrationBuilder.RenameIndex(
				name: "IX_task_submissions_DecidedByAdminId",
				table: "task_submissions",
				newName: "IX_task_submissions_DecidedByCoordinatorId");

			migrationBuilder.RenameColumn(
				name: "AdminUserId",
				table: "task_trusted_coordinators",
				newName: "CoordinatorUserId");

			migrationBuilder.RenameIndex(
				name: "IX_task_trusted_admins_AdminUserId",
				table: "task_trusted_coordinators",
				newName: "IX_task_trusted_coordinators_CoordinatorUserId");

			migrationBuilder.AddPrimaryKey(
				name: "PK_task_trusted_coordinators",
				table: "task_trusted_coordinators",
				columns: new[] { "TaskId", "CoordinatorUserId" });

			migrationBuilder.AddForeignKey(
				name: "FK_task_submissions_users_DecidedByCoordinatorId",
				table: "task_submissions",
				column: "DecidedByCoordinatorId",
				principalTable: "users",
				principalColumn: "Id",
				onDelete: ReferentialAction.SetNull);

			migrationBuilder.AddForeignKey(
				name: "FK_task_trusted_coordinators_tasks_TaskId",
				table: "task_trusted_coordinators",
				column: "TaskId",
				principalTable: "tasks",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "FK_task_trusted_coordinators_users_CoordinatorUserId",
				table: "task_trusted_coordinators",
				column: "CoordinatorUserId",
				principalTable: "users",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade);
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_task_submissions_users_DecidedByCoordinatorId",
				table: "task_submissions");

			migrationBuilder.DropForeignKey(
				name: "FK_task_trusted_coordinators_tasks_TaskId",
				table: "task_trusted_coordinators");

			migrationBuilder.DropForeignKey(
				name: "FK_task_trusted_coordinators_users_CoordinatorUserId",
				table: "task_trusted_coordinators");

			migrationBuilder.DropPrimaryKey(
				name: "PK_task_trusted_coordinators",
				table: "task_trusted_coordinators");

			migrationBuilder.RenameColumn(
				name: "DecidedByCoordinatorId",
				table: "task_submissions",
				newName: "DecidedByAdminId");

			migrationBuilder.RenameIndex(
				name: "IX_task_submissions_DecidedByCoordinatorId",
				table: "task_submissions",
				newName: "IX_task_submissions_DecidedByAdminId");

			migrationBuilder.RenameColumn(
				name: "CoordinatorUserId",
				table: "task_trusted_coordinators",
				newName: "AdminUserId");

			migrationBuilder.RenameIndex(
				name: "IX_task_trusted_coordinators_CoordinatorUserId",
				table: "task_trusted_coordinators",
				newName: "IX_task_trusted_admins_AdminUserId");

			migrationBuilder.RenameTable(
				name: "task_trusted_coordinators",
				newName: "task_trusted_admins");

			migrationBuilder.AddPrimaryKey(
				name: "PK_task_trusted_admins",
				table: "task_trusted_admins",
				columns: new[] { "TaskId", "AdminUserId" });

			migrationBuilder.AddForeignKey(
				name: "FK_task_submissions_users_DecidedByAdminId",
				table: "task_submissions",
				column: "DecidedByAdminId",
				principalTable: "users",
				principalColumn: "Id",
				onDelete: ReferentialAction.SetNull);

			migrationBuilder.AddForeignKey(
				name: "FK_task_trusted_admins_tasks_TaskId",
				table: "task_trusted_admins",
				column: "TaskId",
				principalTable: "tasks",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "FK_task_trusted_admins_users_AdminUserId",
				table: "task_trusted_admins",
				column: "AdminUserId",
				principalTable: "users",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade);
		}
	}
}