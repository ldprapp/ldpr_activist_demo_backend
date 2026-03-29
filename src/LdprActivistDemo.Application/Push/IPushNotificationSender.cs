namespace LdprActivistDemo.Application.Push;

public interface IPushNotificationSender
{
	Task<PushSendResult> SendManyAsync(
		IReadOnlyList<PushTargetModel> targets,
		PushMessage message,
		CancellationToken cancellationToken);
}

public sealed record PushMessage(
	string Title,
	string Body,
	IReadOnlyDictionary<string, string>? Data = null);

public sealed record PushSendResult(
	int SuccessCount,
	int FailureCount,
	IReadOnlyList<string> InvalidTokens)
{
	public static PushSendResult Empty { get; } = new(
		0,
		0,
		Array.Empty<string>());
}