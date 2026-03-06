using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public interface IActorAccessService
{
	Task<ActorAuthenticationResult> AuthenticateAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
}

public enum ActorAuthenticationError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
}

public readonly record struct ActorAuthenticationResult(UserInternalModel? Actor, ActorAuthenticationError Error)
{
	public bool IsSuccess => Error == ActorAuthenticationError.None;

	public static ActorAuthenticationResult Success(UserInternalModel actor) => new(actor, ActorAuthenticationError.None);

	public static ActorAuthenticationResult Fail(ActorAuthenticationError error) => new(null, error);
}