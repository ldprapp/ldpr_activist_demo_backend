using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	/// <inheritdoc />
	public partial class AddReferralRewardPointsSettings : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "InvitedUserRewardPoints",
				table: "referral_settings",
				type: "integer",
				nullable: false,
				defaultValue: 100);

			migrationBuilder.AddColumn<int>(
				name: "InviterRewardPoints",
				table: "referral_settings",
				type: "integer",
				nullable: false,
				defaultValue: 100);

			migrationBuilder.AddCheckConstraint(
				name: "ck_referral_settings_invited_user_reward_points_non_negative",
				table: "referral_settings",
				sql: "\"InvitedUserRewardPoints\" >= 0");

			migrationBuilder.AddCheckConstraint(
				name: "ck_referral_settings_inviter_reward_points_non_negative",
				table: "referral_settings",
				sql: "\"InviterRewardPoints\" >= 0");

			migrationBuilder.Sql(
				"""
				INSERT INTO referral_settings ("Id", "InviteTextTemplate", "InviterRewardPoints", "InvitedUserRewardPoints")
				SELECT
					1,
					'Присоединяйтесь к приложению «ЛДПР Активист». При регистрации укажите мой реферальный код {code} и получите 100 баллов в подарок.',
					100,
					100
				WHERE NOT EXISTS (
					SELECT 1
					FROM referral_settings
					WHERE "Id" = 1
				);
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_referral_settings_invited_user_reward_points_non_negative",
				table: "referral_settings");

			migrationBuilder.DropCheckConstraint(
				name: "ck_referral_settings_inviter_reward_points_non_negative",
				table: "referral_settings");

			migrationBuilder.DropColumn(
				name: "InvitedUserRewardPoints",
				table: "referral_settings");

			migrationBuilder.DropColumn(
				name: "InviterRewardPoints",
				table: "referral_settings");
		}
	}
}
