using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	/// <inheritdoc />
	public partial class AddUserReferralCode : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "ReferralCode",
				table: "users",
				type: "integer",
				nullable: true);

			migrationBuilder.Sql(
				"""
                DO $$
                DECLARE
                    users_count integer;
                BEGIN
                    SELECT COUNT(*)
                    INTO users_count
                    FROM users;

                    IF users_count > 900000 THEN
                        RAISE EXCEPTION
                            'Cannot assign unique 6-digit referral codes: users count (%) exceeds 900000.',
                            users_count;
                    END IF;
                END
                $$;
                """);

			migrationBuilder.Sql(
				"""
                WITH numbered_users AS
                (
                    SELECT
                        "Id",
                        100000 + ROW_NUMBER() OVER (ORDER BY random(), "Id") - 1 AS referral_code
                    FROM users
                )
                UPDATE users AS u
                SET "ReferralCode" = numbered_users.referral_code
                FROM numbered_users
                WHERE u."Id" = numbered_users."Id";
                """);

			migrationBuilder.AlterColumn<int>(
				name: "ReferralCode",
				table: "users",
				type: "integer",
				nullable: false,
				oldClrType: typeof(int),
				oldType: "integer",
				oldNullable: true);

			migrationBuilder.AddCheckConstraint(
				name: "ck_users_referral_code_range",
				table: "users",
				sql: "\"ReferralCode\" >= 100000 AND \"ReferralCode\" <= 999999");

			migrationBuilder.CreateIndex(
				name: "ix_users_referral_code",
				table: "users",
				column: "ReferralCode",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_users_referral_code",
				table: "users");

			migrationBuilder.DropCheckConstraint(
				name: "ck_users_referral_code_range",
				table: "users");

			migrationBuilder.DropColumn(
				name: "ReferralCode",
				table: "users");
		}
	}
}
