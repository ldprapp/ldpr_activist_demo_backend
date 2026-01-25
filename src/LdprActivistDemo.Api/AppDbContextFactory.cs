using LdprActivistDemo.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LdprActivistDemo.Api;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
	public AppDbContext CreateDbContext(string[] args)
	{
		var environmentName =
Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
"Development";

		var currentDir = Directory.GetCurrentDirectory();

		var apiProjectPathCandidate = Path.GetFullPath(
Path.Combine(currentDir, "src", "LdprActivistDemo.Api"));

		var apiProjectPath = File.Exists(Path.Combine(apiProjectPathCandidate, "appsettings.json"))
		   ? apiProjectPathCandidate
		   : currentDir;

		var config = new ConfigurationBuilder()
		   .SetBasePath(apiProjectPath)
		   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
		   .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
		   .AddEnvironmentVariables()
		   .Build();

		var cs = config.GetConnectionString("Postgres") ?? config["ConnectionStrings:Postgres"];
		if(string.IsNullOrWhiteSpace(cs))
		{
			throw new InvalidOperationException(
				"Postgres connection string is not configured (ConnectionStrings:Postgres). " +
				"Set ConnectionStrings__Postgres environment variable or add it to appsettings.");
		}

		var options = new DbContextOptionsBuilder<AppDbContext>()
		   .UseNpgsql(cs, npgsql => npgsql.MigrationsAssembly("LdprActivistDemo.Migrations"))
		   .Options;

		return new AppDbContext(options);
	}
}