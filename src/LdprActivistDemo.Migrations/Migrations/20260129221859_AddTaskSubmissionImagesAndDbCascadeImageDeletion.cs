using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskSubmissionImagesAndDbCascadeImageDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoImageIds",
                table: "task_submissions");

            migrationBuilder.CreateTable(
                name: "task_submission_images",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_submission_images", x => new { x.SubmissionId, x.ImageId });
                    table.ForeignKey(
                        name: "FK_task_submission_images_images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_submission_images_task_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "task_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_submission_images_ImageId",
                table: "task_submission_images",
                column: "ImageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_submission_images");

            migrationBuilder.AddColumn<Guid[]>(
                name: "PhotoImageIds",
                table: "task_submissions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");
        }
    }
}
