using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations;

public partial class BackfillExistingUsersIntoUserRatings : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			INSERT INTO user_ratings ("UserId", "OverallRank", "RegionRank", "SettlementRank")
			SELECT
				u."Id",
				NULL,
				NULL,
				NULL
			FROM users u
			LEFT JOIN user_ratings ur ON ur."UserId" = u."Id"
			WHERE ur."UserId" IS NULL;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			DELETE FROM user_ratings ur
			WHERE ur."OverallRank" IS NULL
			  AND ur."RegionRank" IS NULL
			  AND ur."SettlementRank" IS NULL
			  AND EXISTS (
				SELECT 1
				FROM users u
				WHERE u."Id" = ur."UserId"
			  );
			""");
	}
}