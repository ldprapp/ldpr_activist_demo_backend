namespace LdprActivistDemo.Persistence;

public sealed class UserRatingsRefreshState
{
	public string JobName { get; set; } = string.Empty;

	public int ScheduledHour { get; set; } = 4;

	public int ScheduledMinute { get; set; } = 0;

	public DateOnly? LastCompletedLocalDate { get; set; }

	public DateTimeOffset? LastCompletedAtUtc { get; set; }
}