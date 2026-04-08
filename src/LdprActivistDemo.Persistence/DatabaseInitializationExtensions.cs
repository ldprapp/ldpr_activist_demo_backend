using LdprActivistDemo.Application.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LdprActivistDemo.Persistence;

public static class DatabaseInitializationExtensions
{
	public static async Task InitializePersistenceAsync(
		this IServiceProvider services,
		bool autoMigrate,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(services);

		using var scope = services.CreateScope();
		var serviceProvider = scope.ServiceProvider;

		var db = serviceProvider.GetRequiredService<AppDbContext>();
		var userRepository = serviceProvider.GetRequiredService<IUserRepository>();
		var schemaInspector = serviceProvider.GetRequiredService<IDatabaseSchemaInspector>();

		if(autoMigrate)
		{
			await db.Database.MigrateAsync(cancellationToken);
		}

		var inspection = await schemaInspector.InspectAsync(cancellationToken);
		if(!inspection.CanConnect)
		{
			throw new InvalidOperationException(
				"Cannot connect to PostgreSQL. Check the connection string and database availability.");
		}

		if(!inspection.IsInitialized)
		{
			var appliedMigrations = inspection.AppliedMigrations.Count == 0
				? "<none>"
				: string.Join(", ", inspection.AppliedMigrations);

			var pendingMigrations = inspection.PendingMigrations.Count == 0
				? "<none>"
				: string.Join(", ", inspection.PendingMigrations);

			throw new InvalidOperationException(
				"Database schema is not initialized. Table 'users' was not found. " +
				$"AutoMigrate={autoMigrate}. " +
				$"AppliedMigrations={appliedMigrations}. " +
				$"PendingMigrations={pendingMigrations}. " +
				"For a fresh environment either enable Database:AutoMigrate or apply EF Core migrations manually before starting the API. " +
				"If AutoMigrate=true and 'users' is still missing, check that LdprActivistDemo.Migrations contains real Migration classes, not only AppDbContextModelSnapshot.");
		}

		_ = await userRepository.DeleteAllUnconfirmedAsync(cancellationToken);
	}
}