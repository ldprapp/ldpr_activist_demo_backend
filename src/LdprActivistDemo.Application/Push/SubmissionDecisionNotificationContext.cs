namespace LdprActivistDemo.Application.Push;

/// <summary>
/// Описывает контекст уведомления о решении по заявке.
/// </summary>
public sealed record SubmissionDecisionNotificationContext(
	Guid SubmissionId,
	Guid TaskId,
	Guid UserId,
	string TaskTitle,
	string DecisionStatus);