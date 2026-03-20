using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddBannedUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role_allowed",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role_allowed",
                table: "users",
                sql: "\"Role\" IN ('activist','coordinator','admin','banned')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role_allowed",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role_allowed",
                table: "users",
                sql: "\"Role\" IN ('activist','coordinator','admin')");
        }
    }
}
