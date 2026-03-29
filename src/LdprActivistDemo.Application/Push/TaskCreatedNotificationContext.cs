namespace LdprActivistDemo.Application.Push;

/// <summary>
/// Описывает контекст уведомления о создании задачи.
/// </summary>
public sealed record TaskCreatedNotificationContext(
	Guid TaskId,
	string TaskTitle,
	int RegionId,
	int? SettlementId);