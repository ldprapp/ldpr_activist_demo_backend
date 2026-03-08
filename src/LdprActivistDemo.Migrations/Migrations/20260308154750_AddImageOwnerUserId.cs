using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddImageOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "images",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_images_OwnerUserId",
                table: "images",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_images_users_OwnerUserId",
                table: "images",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_images_users_OwnerUserId",
                table: "images");

            migrationBuilder.DropIndex(
                name: "IX_images_OwnerUserId",
                table: "images");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "images");
        }
    }
}
