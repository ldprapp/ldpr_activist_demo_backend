using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LdprActivistDemo.Migrations.Migrations
{
	/// <inheritdoc />
	public partial class AddReferralInviteTextTemplate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "referral_settings",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false),
					InviteTextTemplate = table.Column<string>(type: "text", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_referral_settings", x => x.Id);
					table.CheckConstraint("ck_referral_settings_invite_text_template_has_code", "position('{code}' in \"InviteTextTemplate\") > 0");
					table.CheckConstraint("ck_referral_settings_singleton_id", "\"Id\" = 1");
				});

			migrationBuilder.InsertData(
				table: "referral_settings",
				columns: new[] { "Id", "InviteTextTemplate" },
				values: new object[]
				{
					1,
					"Присоединяйтесь к приложению «ЛДПР Активист». При регистрации укажите мой реферальный код {code} и получите 100 баллов в подарок."
				});
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "referral_settings");
		}
	}
}
