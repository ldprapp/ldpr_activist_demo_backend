using LdprActivistDemo.Contracts.Push;

namespace LdprActivistDemo.Persistence;

public sealed class UserPushDevice
{
	public Guid Id { get; set; }

	public Guid UserId { get; set; }

	public User User { get; set; } = null!;

	public string Token { get; set; } = string.Empty;

	public string Platform { get; set; } = PushPlatform.Android;

	public bool IsActive { get; set; } = true;

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }
}