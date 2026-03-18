using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	/// <inheritdoc />
	public partial class RenameCityToSettlement : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_tasks_cities_CityId",
				table: "tasks");

			migrationBuilder.DropForeignKey(
				name: "FK_users_cities_CityId",
				table: "users");

			migrationBuilder.DropForeignKey(
				name: "FK_cities_regions_RegionId",
				table: "cities");

			migrationBuilder.DropPrimaryKey(
				name: "PK_cities",
				table: "cities");

			migrationBuilder.DropIndex(
				name: "IX_system_images_ImageId",
				table: "system_images");

			migrationBuilder.RenameColumn(
				name: "CityId",
				table: "users",
				newName: "SettlementId");

			migrationBuilder.RenameTable(
				name: "cities",
				newName: "settlements");

			migrationBuilder.RenameIndex(
				name: "IX_users_CityId",
				table: "users",
				newName: "IX_users_SettlementId");

			migrationBuilder.RenameColumn(
				name: "CityId",
				table: "tasks",
				newName: "SettlementId");

			migrationBuilder.RenameIndex(
				name: "IX_tasks_RegionId_CityId_Status_DeadlineAt",
				table: "tasks",
				newName: "IX_tasks_RegionId_SettlementId_Status_DeadlineAt");

			migrationBuilder.RenameIndex(
				name: "IX_tasks_CityId",
				table: "tasks",
				newName: "IX_tasks_SettlementId");

			migrationBuilder.RenameIndex(
				name: "IX_cities_RegionId_Name",
				table: "settlements",
				newName: "IX_settlements_RegionId_Name");

			migrationBuilder.AlterColumn<bool>(
				name: "IsDeleted",
				table: "regions",
				type: "boolean",
				nullable: false,
				oldClrType: typeof(bool),
				oldType: "boolean",
				oldDefaultValue: false);

			migrationBuilder.AddPrimaryKey(
				name: "PK_settlements",
				table: "settlements",
				column: "Id");

			migrationBuilder.CreateIndex(
				name: "IX_system_images_ImageId",
				table: "system_images",
				column: "ImageId");

			migrationBuilder.AddForeignKey(
				name: "FK_settlements_regions_RegionId",
				table: "settlements",
				column: "RegionId",
				principalTable: "regions",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "FK_tasks_settlements_SettlementId",
				table: "tasks",
				column: "SettlementId",
				principalTable: "settlements",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "FK_users_settlements_SettlementId",
				table: "users",
				column: "SettlementId",
				principalTable: "settlements",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_tasks_settlements_SettlementId",
				table: "tasks");

			migrationBuilder.DropForeignKey(
				name: "FK_users_settlements_SettlementId",
				table: "users");

			migrationBuilder.DropForeignKey(
				name: "FK_settlements_regions_RegionId",
				table: "settlements");

			migrationBuilder.DropPrimaryKey(
				name: "PK_settlements",
				table: "settlements");

			migrationBuilder.DropIndex(
				name: "IX_system_images_ImageId",
				table: "system_images");

			migrationBuilder.RenameColumn(
				name: "SettlementId",
				table: "users",
				newName: "CityId");

			migrationBuilder.RenameIndex(
				name: "IX_users_SettlementId",
				table: "users",
				newName: "IX_users_CityId");

			migrationBuilder.RenameColumn(
				name: "SettlementId",
				table: "tasks",
				newName: "CityId");

			migrationBuilder.RenameIndex(
				name: "IX_tasks_SettlementId",
				table: "tasks",
				newName: "IX_tasks_CityId");

			migrationBuilder.RenameIndex(
				name: "IX_tasks_RegionId_SettlementId_Status_DeadlineAt",
				table: "tasks",
				newName: "IX_tasks_RegionId_CityId_Status_DeadlineAt");

			migrationBuilder.RenameIndex(
				name: "IX_settlements_RegionId_Name",
				table: "settlements",
				newName: "IX_cities_RegionId_Name");

			migrationBuilder.RenameTable(
				name: "settlements",
				newName: "cities");

			migrationBuilder.AlterColumn<bool>(
				name: "IsDeleted",
				table: "regions",
				type: "boolean",
				nullable: false,
				defaultValue: false,
				oldClrType: typeof(bool),
				oldType: "boolean");

			migrationBuilder.AddPrimaryKey(
				name: "PK_cities",
				table: "cities",
				column: "Id");

			migrationBuilder.CreateIndex(
				name: "IX_system_images_ImageId",
				table: "system_images",
				column: "ImageId",
				unique: true);

			migrationBuilder.AddForeignKey(
				name: "FK_cities_regions_RegionId",
				table: "cities",
				column: "RegionId",
				principalTable: "regions",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade);

			migrationBuilder.AddForeignKey(
				name: "FK_tasks_cities_CityId",
				table: "tasks",
				column: "CityId",
				principalTable: "cities",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "FK_users_cities_CityId",
				table: "users",
				column: "CityId",
				principalTable: "cities",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);
		}
	}
}
