using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersGenderCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_users_gender_allowed",
                table: "users",
                sql: "\"Gender\" IS NULL OR \"Gender\" IN ('male','female')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_gender_allowed",
                table: "users");
        }
    }
}
