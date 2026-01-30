using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class UseImageIdsForTasksAndSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "PhotosJson",
                table: "task_submissions");

            migrationBuilder.AddColumn<Guid>(
                name: "CoverImageId",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "PhotoImageIds",
                table: "task_submissions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_CoverImageId",
                table: "tasks",
                column: "CoverImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_images_CoverImageId",
                table: "tasks",
                column: "CoverImageId",
                principalTable: "images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_images_CoverImageId",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_tasks_CoverImageId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "CoverImageId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "PhotoImageIds",
                table: "task_submissions");

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotosJson",
                table: "task_submissions",
                type: "text",
                nullable: true);
        }
    }
}
