#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace LdprActivistDemo.Migrations.Migrations
{
	public partial class AddCancellationFieldsToUserPointsTransactions : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "CancellationComment",
				table: "user_points_transactions",
				type: "text",
				nullable: false,
				defaultValue: "");

			migrationBuilder.AddColumn<bool>(
				name: "IsCancelled",
				table: "user_points_transactions",
				type: "boolean",
				nullable: false,
				defaultValue: false);
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "CancellationComment",
				table: "user_points_transactions");

			migrationBuilder.DropColumn(
				name: "IsCancelled",
				table: "user_points_transactions");
		}
	}
}