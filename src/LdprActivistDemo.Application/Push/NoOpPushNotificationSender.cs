using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Push;

public sealed class NoOpPushNotificationSender : IPushNotificationSender
{
	private readonly ILogger<NoOpPushNotificationSender> _logger;

	public NoOpPushNotificationSender(ILogger<NoOpPushNotificationSender> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task<PushSendResult> SendManyAsync(
		IReadOnlyList<PushTargetModel> targets,
		PushMessage message,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("TargetCount", targets?.Count ?? 0),
			("Title", message?.Title),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Push.Sender.Send,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.SendNoOp,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Push.Sender.Send,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.SendNoOp,
			"No-op push sender started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Push.Sender.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.SendNoOp,
				"No-op push sender completed.",
				properties);

			return Task.FromResult(PushSendResult.Empty);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Push.Sender.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.SendNoOp,
				"No-op push sender aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Push.Sender.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.SendNoOp,
				"No-op push sender failed.",
				ex,
				properties);
			throw;
		}
	}
}