using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Contracts.Push;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Push;

public sealed class PushDeviceService : IPushDeviceService
{
	private readonly IPushDeviceRepository _devices;
	private readonly IActorAccessService _actorAccess;
	private readonly ILogger<PushDeviceService> _logger;

	public PushDeviceService(
		IPushDeviceRepository devices,
		IActorAccessService actorAccess,
		ILogger<PushDeviceService> logger)
	{
		_devices = devices ?? throw new ArgumentNullException(nameof(devices));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<PushDeviceOperationResult> RegisterAsync(
		Guid actorUserId,
		string actorUserPassword,
		string token,
		string platform,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("Platform", NormalizePlatformOrNull(platform)),
			("Token", MaskToken(token)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Push.RegisterDevice,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.RegisterDevice,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Push.RegisterDevice,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.RegisterDevice,
			"Push device registration started.",
			properties);

		try
		{
			var validationError = ValidateRegisterArguments(actorUserId, actorUserPassword, token, platform);
			if(validationError != PushDeviceOperationError.None)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Push.RegisterDevice,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Push.RegisterDevice,
					"Push device registration rejected by validation.",
					StructuredLog.Combine(properties, ("Error", validationError)));

				return PushDeviceOperationResult.Fail(validationError);
			}

			var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
			if(authError is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Push.RegisterDevice,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Push.RegisterDevice,
					"Push device registration rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", authError.Value)));

				return PushDeviceOperationResult.Fail(authError.Value);
			}

			await _devices.UpsertAsync(
				actorUserId,
				token.Trim(),
				NormalizePlatform(platform),
				DateTimeOffset.UtcNow,
				cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Push.RegisterDevice,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.RegisterDevice,
				"Push device registration completed.",
				properties);

			return PushDeviceOperationResult.Success();
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Push.RegisterDevice,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.RegisterDevice,
				"Push device registration aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Push.RegisterDevice,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.RegisterDevice,
				"Push device registration failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<PushDeviceOperationResult> DeactivateAsync(
		Guid actorUserId,
		string actorUserPassword,
		string token,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("Token", MaskToken(token)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Push.DeactivateDevice,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.DeactivateDevice,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Push.DeactivateDevice,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.DeactivateDevice,
			"Push device deactivation started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Push.DeactivateDevice,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Push.DeactivateDevice,
					"Push device deactivation rejected by validation.",
					StructuredLog.Combine(properties, ("Error", PushDeviceOperationError.ValidationFailed)));

				return PushDeviceOperationResult.Fail(PushDeviceOperationError.ValidationFailed);
			}

			if(string.IsNullOrWhiteSpace(token))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Push.DeactivateDevice,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Push.DeactivateDevice,
					"Push device deactivation rejected by validation.",
					StructuredLog.Combine(properties, ("Error", PushDeviceOperationError.TokenInvalid)));

				return PushDeviceOperationResult.Fail(PushDeviceOperationError.TokenInvalid);
			}

			var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
			if(authError is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Push.DeactivateDevice,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Push.DeactivateDevice,
					"Push device deactivation rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", authError.Value)));

				return PushDeviceOperationResult.Fail(authError.Value);
			}

			await _devices.DeactivateAsync(
				actorUserId,
				token.Trim(),
				DateTimeOffset.UtcNow,
				cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Push.DeactivateDevice,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.DeactivateDevice,
				"Push device deactivation completed.",
				properties);

			return PushDeviceOperationResult.Success();
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Push.DeactivateDevice,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.DeactivateDevice,
				"Push device deactivation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Push.DeactivateDevice,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.DeactivateDevice,
				"Push device deactivation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<PushDeviceOperationError?> TryAuthenticateActorAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(auth.IsSuccess)
		{
			return null;
		}

		return auth.Error == ActorAuthenticationError.ValidationFailed
			? PushDeviceOperationError.ValidationFailed
			: PushDeviceOperationError.InvalidCredentials;
	}

	private static PushDeviceOperationError ValidateRegisterArguments(
		Guid actorUserId,
		string actorUserPassword,
		string token,
		string platform)
	{
		if(actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return PushDeviceOperationError.ValidationFailed;
		}

		if(string.IsNullOrWhiteSpace(token))
		{
			return PushDeviceOperationError.TokenInvalid;
		}

		return NormalizePlatformOrNull(platform) is null
			? PushDeviceOperationError.PlatformInvalid
			: PushDeviceOperationError.None;
	}

	private static string NormalizePlatform(string? value)
		=> NormalizePlatformOrNull(value) ?? throw new ArgumentException("Push platform is invalid.", nameof(value));

	private static string? NormalizePlatformOrNull(string? value)
	{
		var token = (value ?? string.Empty).Trim().ToLowerInvariant();

		return token switch
		{
			PushPlatform.Android => PushPlatform.Android,
			PushPlatform.Ios => PushPlatform.Ios,
			PushPlatform.Web => PushPlatform.Web,
			_ => null,
		};
	}

	private static string MaskToken(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		if(normalized.Length <= 8)
		{
			return "****";
		}

		return $"***{normalized[^8..]}";
	}
}