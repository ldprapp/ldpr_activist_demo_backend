using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations;

public partial class AddUserRatingsRefreshScheduleColumns : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(
			name: "ScheduledHour",
			table: "user_ratings_refresh_state",
			type: "integer",
			nullable: false,
			defaultValue: 4);

		migrationBuilder.AddColumn<int>(
			name: "ScheduledMinute",
			table: "user_ratings_refresh_state",
			type: "integer",
			nullable: false,
			defaultValue: 0);

		migrationBuilder.AddCheckConstraint(
			name: "ck_user_ratings_refresh_state_hour_range",
			table: "user_ratings_refresh_state",
			sql: "\"ScheduledHour\" >= 0 AND \"ScheduledHour\" <= 23");

		migrationBuilder.AddCheckConstraint(
			name: "ck_user_ratings_refresh_state_minute_range",
			table: "user_ratings_refresh_state",
			sql: "\"ScheduledMinute\" >= 0 AND \"ScheduledMinute\" <= 59");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropCheckConstraint(
			name: "ck_user_ratings_refresh_state_hour_range",
			table: "user_ratings_refresh_state");

		migrationBuilder.DropCheckConstraint(
			name: "ck_user_ratings_refresh_state_minute_range",
			table: "user_ratings_refresh_state");

		migrationBuilder.DropColumn(
			name: "ScheduledHour",
			table: "user_ratings_refresh_state");

		migrationBuilder.DropColumn(
			name: "ScheduledMinute",
			table: "user_ratings_refresh_state");
	}
}