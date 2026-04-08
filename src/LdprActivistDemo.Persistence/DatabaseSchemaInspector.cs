using System.Data;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence;

public interface IDatabaseSchemaInspector
{
	Task<DatabaseSchemaInspectionResult> InspectAsync(CancellationToken cancellationToken);
}

public sealed record DatabaseSchemaInspectionResult(
	bool CanConnect,
	bool UsersTableExists,
	IReadOnlyList<string> AppliedMigrations,
	IReadOnlyList<string> PendingMigrations)
{
	public bool IsInitialized => UsersTableExists;
}

public sealed class DatabaseSchemaInspector : IDatabaseSchemaInspector
{
	private readonly AppDbContext _db;

	public DatabaseSchemaInspector(AppDbContext db)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
	}

	public async Task<DatabaseSchemaInspectionResult> InspectAsync(CancellationToken cancellationToken)
	{
		var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
		if(!canConnect)
		{
			return new DatabaseSchemaInspectionResult(
				CanConnect: false,
				UsersTableExists: false,
				AppliedMigrations: Array.Empty<string>(),
				PendingMigrations: Array.Empty<string>());
		}

		var appliedMigrations = (await _db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
		var pendingMigrations = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
		var usersTableExists = await TableExistsAsync("users", cancellationToken);

		return new DatabaseSchemaInspectionResult(
			CanConnect: true,
			UsersTableExists: usersTableExists,
			AppliedMigrations: appliedMigrations,
			PendingMigrations: pendingMigrations);
	}

	private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
	{
		var connection = _db.Database.GetDbConnection();

		var shouldCloseConnection = connection.State != ConnectionState.Open;
		if(shouldCloseConnection)
		{
			await connection.OpenAsync(cancellationToken);
		}

		try
		{
			await using var command = connection.CreateCommand();
			command.CommandText =
				"""
				select exists (
				    select 1
				    from information_schema.tables
				    where table_schema = current_schema()
				      and table_name = @table_name
				);
				""";

			var parameter = command.CreateParameter();
			parameter.ParameterName = "@table_name";
			parameter.Value = tableName;
			command.Parameters.Add(parameter);

			var scalar = await command.ExecuteScalarAsync(cancellationToken);
			return scalar is bool exists && exists;
		}
		finally
		{
			if(shouldCloseConnection)
			{
				await connection.CloseAsync();
			}
		}
	}
}