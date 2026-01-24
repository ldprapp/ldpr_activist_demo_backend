namespace LdprActivistDemo.Persistence;

public sealed class TaskSubmission
{
	public Guid Id { get; set; }

	public Guid TaskId { get; set; }

	public TaskEntity Task { get; set; } = null!;

	public Guid UserId { get; set; }

	public User User { get; set; } = null!;

	public DateTimeOffset SubmittedAt { get; set; }

	public Guid? ConfirmedByAdminId { get; set; }

	public User? ConfirmedByAdmin { get; set; }

	public DateTimeOffset? ConfirmedAt { get; set; }

	/// <summary>
	/// JSON-массив строк (URL/пути к фото). Для демо храним сырой json, без конвертеров.
	/// </summary>
	public string? PhotosJson { get; set; }

	public string? ProofText { get; set; }
}