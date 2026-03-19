using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Users;

public sealed class ActorAccessService : IActorAccessService
{
	private readonly IUserRepository _users;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ILogger<ActorAccessService> _logger;

	public ActorAccessService(
		IUserRepository users,
		IPasswordHasher passwordHasher,
		ILogger<ActorAccessService> logger)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ActorAuthenticationResult> AuthenticateAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Auth.ActorAuthenticate,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Auth.AuthenticateActor,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Auth.ActorAuthenticate,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Auth.AuthenticateActor,
			"Actor authentication started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Auth.ActorAuthenticate,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Auth.AuthenticateActor,
					"Actor authentication rejected by validation.",
					StructuredLog.Combine(properties, ("Error", ActorAuthenticationError.ValidationFailed)));

				return ActorAuthenticationResult.Fail(ActorAuthenticationError.ValidationFailed);
			}

			var actor = await _users.GetInternalByIdAsync(actorUserId, cancellationToken);
			if(actor is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Auth.ActorAuthenticate,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Auth.AuthenticateActor,
					"Actor authentication rejected. Actor not found.",
					StructuredLog.Combine(properties, ("Error", ActorAuthenticationError.InvalidCredentials)));

				return ActorAuthenticationResult.Fail(ActorAuthenticationError.InvalidCredentials);
			}

			cancellationToken.ThrowIfCancellationRequested();

			var ok = _passwordHasher.Verify(actor.PasswordHash, actorUserPassword);
			if(!ok)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Auth.ActorAuthenticate,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Auth.AuthenticateActor,
					"Actor authentication rejected. Invalid password.",
					StructuredLog.Combine(properties, ("Error", ActorAuthenticationError.InvalidCredentials)));

				return ActorAuthenticationResult.Fail(ActorAuthenticationError.InvalidCredentials);
			}

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Auth.ActorAuthenticate,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Auth.AuthenticateActor,
				"Actor authentication completed.",
				StructuredLog.Combine(properties, ("ActorRole", actor.Role)));

			return ActorAuthenticationResult.Success(actor);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Auth.ActorAuthenticate,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Auth.AuthenticateActor,
				"Actor authentication aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Auth.ActorAuthenticate,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Auth.AuthenticateActor,
				"Actor authentication failed.",
				ex,
				properties);
			throw;
		}
	}
}