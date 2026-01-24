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

	public string? CoverImageUrl { get; set; }

	public string? ExecutionLocation { get; set; }

	public DateTimeOffset PublishedAt { get; set; }

	public DateTimeOffset DeadlineAt { get; set; }

	public TaskStatus Status { get; set; }

	public int RegionId { get; set; }

	public Region Region { get; set; } = null!;

	public int? CityId { get; set; }

	public City? City { get; set; }

	public List<TaskTrustedAdmin> TrustedAdmins { get; set; } = new();
}