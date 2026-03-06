namespace LdprActivistDemo.Application.Users;

public sealed class ActorAccessService : IActorAccessService
{
	private readonly IUserRepository _users;

	public ActorAccessService(IUserRepository users)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
	}

	public async Task<ActorAuthenticationResult> AuthenticateAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		if(actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return ActorAuthenticationResult.Fail(ActorAuthenticationError.ValidationFailed);
		}

		var actor = await _users.GetInternalByIdAsync(actorUserId, cancellationToken);
		if(actor is null)
		{
			return ActorAuthenticationResult.Fail(ActorAuthenticationError.InvalidCredentials);
		}

		var ok = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!ok)
		{
			return ActorAuthenticationResult.Fail(ActorAuthenticationError.InvalidCredentials);
		}

		return ActorAuthenticationResult.Success(actor);
	}
}