using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	public partial class AddUserRatingsRankIndexes : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				CREATE INDEX IF NOT EXISTS "IX_user_ratings_OverallRank_UserId"
				ON user_ratings ("OverallRank", "UserId")
				WHERE "OverallRank" IS NOT NULL;
				""");

			migrationBuilder.Sql(
				"""
				CREATE INDEX IF NOT EXISTS "IX_user_ratings_RegionRank_UserId"
				ON user_ratings ("RegionRank", "UserId")
				WHERE "RegionRank" IS NOT NULL;
				""");

			migrationBuilder.Sql(
				"""
				CREATE INDEX IF NOT EXISTS "IX_user_ratings_SettlementRank_UserId"
				ON user_ratings ("SettlementRank", "UserId")
				WHERE "SettlementRank" IS NOT NULL;
				""");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				DROP INDEX IF EXISTS "IX_user_ratings_OverallRank_UserId";
				""");

			migrationBuilder.Sql(
				"""
				DROP INDEX IF EXISTS "IX_user_ratings_RegionRank_UserId";
				""");

			migrationBuilder.Sql(
				"""
				DROP INDEX IF EXISTS "IX_user_ratings_SettlementRank_UserId";
				""");
		}
	}
}