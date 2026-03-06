using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteRegionCities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cities_regions_RegionId",
                table: "cities");

            migrationBuilder.AddForeignKey(
                name: "FK_cities_regions_RegionId",
                table: "cities",
                column: "RegionId",
                principalTable: "regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cities_regions_RegionId",
                table: "cities");

            migrationBuilder.AddForeignKey(
                name: "FK_cities_regions_RegionId",
                table: "cities",
                column: "RegionId",
                principalTable: "regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
