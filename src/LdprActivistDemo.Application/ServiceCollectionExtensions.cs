using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Users;

using Microsoft.Extensions.DependencyInjection;

namespace LdprActivistDemo.Application;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
		services.AddScoped<IOtpService, OtpService>();

		services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

		services.AddScoped<IGeoDirectoryService, GeoDirectoryService>();
		services.AddScoped<IUserService, UserService>();
		services.AddScoped<ITaskService, TaskService>();
		return services;
	}
}