namespace LdprActivistDemo.Application.Push;

/// <summary>
/// Предоставляет read-model контексты для доменных push-уведомлений задач и заявок.
/// </summary>
public interface ITaskNotificationReadRepository
{
	/// <summary>
	/// Возвращает контекст уведомления о созданной задаче.
	/// </summary>
	Task<TaskCreatedNotificationContext?> GetTaskCreatedContextAsync(
		Guid taskId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Возвращает контекст уведомления о решении по заявке.
	/// </summary>
	Task<SubmissionDecisionNotificationContext?> GetSubmissionDecisionContextAsync(
		Guid submissionId,
		CancellationToken cancellationToken);
}