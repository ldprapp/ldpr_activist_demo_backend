using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Seeding;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LdprActivistDemo.Persistence;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
	{
		var cs = configuration.GetConnectionString("Postgres") ?? configuration["ConnectionStrings:Postgres"];
		if(string.IsNullOrWhiteSpace(cs))
		{
			throw new InvalidOperationException("Postgres connection string is not configured (ConnectionStrings:Postgres).");
		}

		services.AddDbContext<AppDbContext>(options =>
			options.UseNpgsql(cs, npgsql => npgsql.MigrationsAssembly("LdprActivistDemo.Migrations")));

		var redisCs = configuration.GetConnectionString("Redis") ?? configuration["ConnectionStrings:Redis"];
		if(!string.IsNullOrWhiteSpace(redisCs))
		{
			services.AddStackExchangeRedisCache(opts => opts.Configuration = redisCs);
			services.AddSingleton<IOtpStore, RedisOtpStore>();
		}

		services.Configure<GeoSeedOptions>(configuration.GetSection("GeoSeed"));
		services.AddScoped<GeoDbSeeder>();

		services.AddScoped<IRegionRepository, RegionRepository>();
		services.AddScoped<ICityRepository, CityRepository>();
		services.AddScoped<IUserRepository, UserRepository>();
		services.AddScoped<ITaskRepository, TaskRepository>();
		services.AddScoped<ITaskSubmissionRepository, TaskSubmissionRepository>();

		return services;
	}
}