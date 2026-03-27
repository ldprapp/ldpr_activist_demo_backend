using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.PasswordReset;
using LdprActivistDemo.Application.Referrals;
using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.UserPoints;
using LdprActivistDemo.Application.UserRatings;
using LdprActivistDemo.Application.Users;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LdprActivistDemo.Application;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
		services.AddScoped<IOtpService, OtpService>();

		services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

		services.AddScoped<IPasswordResetService, PasswordResetService>();

		services.AddScoped<IActorAccessService, ActorAccessService>();
		services.AddScoped<IReferralService, ReferralService>();
		services.AddScoped<IReferralRewardSettingsService, ReferralRewardSettingsService>();
		services.AddScoped<IGeoDirectoryService, GeoDirectoryService>();
		services.AddScoped<IUserService, UserService>();
		services.AddScoped<IUserPointsService, UserPointsService>();
		services.AddScoped<IUserRatingsService, UserRatingsService>();
		services.AddScoped<IUserRatingsRefreshAdminService, UserRatingsRefreshAdminService>();
		services.AddScoped<ITaskService, TaskService>();
		services.AddScoped<IImageService, ImageService>();

		services.AddSingleton<UnconfirmedUsersCleanupHostedService>();
		services.AddSingleton<IHostedService>(sp =>
			sp.GetRequiredService<UnconfirmedUsersCleanupHostedService>());
		services.AddSingleton<IUnconfirmedUserCleanupScheduler>(sp =>
			sp.GetRequiredService<UnconfirmedUsersCleanupHostedService>());

		services.AddSingleton<UserRatingsRefreshHostedService>();
		services.AddSingleton<IUserRatingsRefreshRuntime>(sp =>
			sp.GetRequiredService<UserRatingsRefreshHostedService>());
		services.AddSingleton<IHostedService>(sp =>
			sp.GetRequiredService<UserRatingsRefreshHostedService>());

		return services;
	}
}