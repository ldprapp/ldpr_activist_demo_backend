using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsAdminWithUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "activist");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role_allowed",
                table: "users",
                sql: "\"Role\" IN ('activist','coordinator','admin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role_allowed",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "users");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
