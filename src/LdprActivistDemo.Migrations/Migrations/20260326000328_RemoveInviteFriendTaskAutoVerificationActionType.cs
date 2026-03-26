using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	/// <inheritdoc />
	public partial class RemoveInviteFriendTaskAutoVerificationActionType : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM tasks
                        WHERE "AutoVerificationActionType" = 'invite_friend'
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot remove autoVerificationActionType=invite_friend while tasks still reference it. Migrate those rows explicitly before applying this migration.';
                    END IF;
                END
                $$;
                """);

			migrationBuilder.DropCheckConstraint(
				name: "ck_tasks_auto_verification_action_type_allowed",
				table: "tasks");

			migrationBuilder.AddCheckConstraint(
				name: "ck_tasks_auto_verification_action_type_allowed",
				table: "tasks",
				sql: "(\"VerificationType\" = 'manual' AND \"AutoVerificationActionType\" IS NULL) OR (\"VerificationType\" = 'auto' AND \"AutoVerificationActionType\" IN ('first_login','auto'))");
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
				sql: "(\"VerificationType\" = 'manual' AND \"AutoVerificationActionType\" IS NULL) OR (\"VerificationType\" = 'auto' AND \"AutoVerificationActionType\" IN ('invite_friend','first_login','auto'))");
		}
	}
}
