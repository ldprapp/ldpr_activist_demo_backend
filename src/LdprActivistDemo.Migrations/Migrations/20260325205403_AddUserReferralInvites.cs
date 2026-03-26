using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReferralInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_referral_invites",
                columns: table => new
                {
                    InvitedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_referral_invites", x => x.InvitedUserId);
                    table.CheckConstraint("ck_user_referral_invites_users_not_equal", "\"InviterUserId\" <> \"InvitedUserId\"");
                    table.ForeignKey(
                        name: "FK_user_referral_invites_users_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_referral_invites_users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_referral_invites_InviterUserId",
                table: "user_referral_invites",
                column: "InviterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_referral_invites");
        }
    }
}
