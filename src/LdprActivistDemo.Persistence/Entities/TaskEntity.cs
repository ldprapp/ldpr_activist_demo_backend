using LdprActivistDemo.Contracts.Tasks;

using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Persistence;

public sealed class TaskEntity
{
	public Guid Id { get; set; }

	public Guid AuthorUserId { get; set; }

	public User AuthorUser { get; set; } = null!;

	public string Title { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string RequirementsText { get; set; } = string.Empty;

	public int RewardPoints { get; set; }

	public Guid? CoverImageId { get; set; }

	public string? ExecutionLocation { get; set; }

	public DateTimeOffset PublishedAt { get; set; }

	public DateTimeOffset? DeadlineAt { get; set; }

	public string Status { get; set; } = TaskStatus.Open;

	public string VerificationType { get; set; } = TaskVerificationType.Manual;

	public string ReuseType { get; set; } = TaskReuseType.Disposable;

	public string? AutoVerificationActionType { get; set; }

	public int RegionId { get; set; }

	public Region Region { get; set; } = null!;

	public int? SettlementId { get; set; }

	public Settlement? Settlement { get; set; }
	public List<TaskTrustedCoordinator> TrustedCoordinators { get; set; } = new();
}