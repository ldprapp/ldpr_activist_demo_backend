namespace LdprActivistDemo.Application.Users;

public sealed class ActorAccessService : IActorAccessService
{
	private readonly IUserRepository _users;
	private readonly IPasswordHasher _passwordHasher;

	public ActorAccessService(IUserRepository users, IPasswordHasher passwordHasher)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
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

		cancellationToken.ThrowIfCancellationRequested();

		var ok = _passwordHasher.Verify(actor.PasswordHash, actorUserPassword);
		if(!ok)
		{
			return ActorAuthenticationResult.Fail(ActorAuthenticationError.InvalidCredentials);
		}

		return ActorAuthenticationResult.Success(actor);
	}
}