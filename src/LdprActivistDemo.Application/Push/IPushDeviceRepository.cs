namespace LdprActivistDemo.Application.Push;

/// <summary>
/// Предоставляет операции хранения и чтения push-устройств пользователей.
/// </summary>
public interface IPushDeviceRepository
{
	/// <summary>
	/// Регистрирует новый token устройства или обновляет уже существующий token.
	/// </summary>
	Task UpsertAsync(
		Guid userId,
		string token,
		string platform,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken);

	/// <summary>
	/// Деактивирует конкретный token устройства пользователя.
	/// </summary>
	Task DeactivateAsync(
		Guid userId,
		string token,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken);

	/// <summary>
	/// Массово деактивирует набор token-ов устройств.
	/// </summary>
	Task DeactivateManyByTokensAsync(
		IReadOnlyCollection<string> tokens,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken);

	/// <summary>
	/// Возвращает активные цели push-уведомлений для конкретного пользователя.
	/// </summary>
	Task<IReadOnlyList<PushTargetModel>> GetActiveTargetsByUserIdAsync(
		Guid userId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Возвращает активные цели push-уведомлений для активистов подходящей географии задачи.
	/// </summary>
	Task<IReadOnlyList<PushTargetModel>> GetActiveTargetsForTaskGeoAsync(
		int regionId,
		int? settlementId,
		CancellationToken cancellationToken);
}