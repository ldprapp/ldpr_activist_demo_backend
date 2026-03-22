namespace LdprActivistDemo.Contracts.Tasks;

public static class TaskSubmissionDecisionStatus
{
	public const string InProgress = "in_progress";
	public const string SubmittedForReview = "submitted_for_review";
	public const string Approve = "approve";
	public const string Rejected = "rejected";
}

/// <summary>
/// Дополнительные режимы фильтрации пользователей задачи для эндпоинта
/// <c>GET /api/v1/tasks/{taskId}/feed/users</c>.
/// </summary>
public static class TaskUsersFeedStatus
{
	/// <summary>
	/// Вернуть всех пользователей подходящей географии независимо от заявок.
	/// </summary>
	public const string All = "all";

	/// <summary>
	/// Вернуть только пользователей подходящей географии без единой заявки по задаче.
	/// </summary>
	public const string NoneSubmit = "none_submit";
}