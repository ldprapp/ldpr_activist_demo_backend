namespace LdprActivistDemo.Application.Push;

public interface IPushDeviceService
{
	Task<PushDeviceOperationResult> RegisterAsync(
		Guid actorUserId,
		string actorUserPassword,
		string token,
		string platform,
		CancellationToken cancellationToken);

	Task<PushDeviceOperationResult> DeactivateAsync(
		Guid actorUserId,
		string actorUserPassword,
		string token,
		CancellationToken cancellationToken);
}

public enum PushDeviceOperationError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	TokenInvalid = 3,
	PlatformInvalid = 4,
}

public readonly record struct PushDeviceOperationResult(PushDeviceOperationError Error)
{
	public bool IsSuccess => Error == PushDeviceOperationError.None;

	public static PushDeviceOperationResult Success()
		=> new(PushDeviceOperationError.None);

	public static PushDeviceOperationResult Fail(PushDeviceOperationError error)
		=> new(error);
}