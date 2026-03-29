using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Contracts.Tasks;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Push;

public sealed class TaskPushNotificationService : ITaskPushNotificationService
{
	private readonly ITaskNotificationReadRepository _notificationReadRepository;
	private readonly IPushDeviceRepository _pushDevices;
	private readonly IPushNotificationSender _sender;
	private readonly ILogger<TaskPushNotificationService> _logger;

	public TaskPushNotificationService(
		ITaskNotificationReadRepository notificationReadRepository,
		IPushDeviceRepository pushDevices,
		IPushNotificationSender sender,
		ILogger<TaskPushNotificationService> logger)
	{
		_notificationReadRepository = notificationReadRepository ?? throw new ArgumentNullException(nameof(notificationReadRepository));
		_pushDevices = pushDevices ?? throw new ArgumentNullException(nameof(pushDevices));
		_sender = sender ?? throw new ArgumentNullException(nameof(sender));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task NotifyTaskCreatedAsync(
		Guid taskId,
		CancellationToken cancellationToken)
	{
		await ExecuteNotificationAsync(
			DomainLogEvents.Push.NotifyTaskCreated,
			ApplicationLogOperations.Push.NotifyTaskCreated,
			new (string Name, object? Value)[]
			{
				("TaskId", taskId),
			},
			async ct =>
			{
				if(taskId == Guid.Empty)
				{
					_logger.LogRejected(
						LogLevel.Warning,
						DomainLogEvents.Push.NotifyTaskCreated,
						LogLayers.ApplicationService,
						ApplicationLogOperations.Push.NotifyTaskCreated,
						"Task created push notification rejected by validation.",
						("TaskId", taskId));
					return;
				}

				var context = await _notificationReadRepository.GetTaskCreatedContextAsync(taskId, ct);
				if(context is null)
				{
					_logger.LogRejected(
						LogLevel.Warning,
						DomainLogEvents.Push.NotifyTaskCreated,
						LogLayers.ApplicationService,
						ApplicationLogOperations.Push.NotifyTaskCreated,
						"Task created push notification rejected. Task context not found.",
						("TaskId", taskId));
					return;
				}

				var targets = await _pushDevices.GetActiveTargetsForTaskGeoAsync(
					context.RegionId,
					context.SettlementId,
					ct);

				if(targets.Count == 0)
				{
					_logger.LogCompleted(
						LogLevel.Debug,
						DomainLogEvents.Push.NotifyTaskCreated,
						LogLayers.ApplicationService,
						ApplicationLogOperations.Push.NotifyTaskCreated,
						"Task created push notification completed. No targets found.",
						("TaskId", context.TaskId),
						("TargetCount", 0));
					return;
				}

				var message = new PushMessage(
					Title: "Новая задача",
					Body: $"Появилась новая задача: {context.TaskTitle}",
					Data: new Dictionary<string, string>(StringComparer.Ordinal)
					{
						["type"] = "task_created",
						["taskId"] = context.TaskId.ToString("D"),
					});

				var sendResult = await _sender.SendManyAsync(targets, message, ct);
				await DeactivateInvalidTokensAsync(
					sendResult.InvalidTokens,
					DomainLogEvents.Push.NotifyTaskCreated,
					ApplicationLogOperations.Push.NotifyTaskCreated,
					ct,
					("TaskId", context.TaskId));

				_logger.LogCompleted(
					LogLevel.Information,
					DomainLogEvents.Push.NotifyTaskCreated,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Push.NotifyTaskCreated,
					"Task created push notification completed.",
					("TaskId", context.TaskId),
					("TargetCount", targets.Count),
					("SuccessCount", sendResult.SuccessCount),
					("FailureCount", sendResult.FailureCount),
					("InvalidTokenCount", sendResult.InvalidTokens.Count));
			},
			cancellationToken);
	}

	public async Task NotifySubmissionApprovedAsync(
		Guid submissionId,
		CancellationToken cancellationToken)
	{
		await NotifySubmissionDecisionAsync(
			submissionId,
			TaskSubmissionDecisionStatus.Approve,
			DomainLogEvents.Push.NotifySubmissionApproved,
			ApplicationLogOperations.Push.NotifySubmissionApproved,
			"Заявка подтверждена",
			context => $"Ваша заявка по задаче \"{context.TaskTitle}\" подтверждена.",
			"submission_approved",
			cancellationToken);
	}

	public async Task NotifySubmissionRejectedAsync(
		Guid submissionId,
		CancellationToken cancellationToken)
	{
		await NotifySubmissionDecisionAsync(
			submissionId,
			TaskSubmissionDecisionStatus.Rejected,
			DomainLogEvents.Push.NotifySubmissionRejected,
			ApplicationLogOperations.Push.NotifySubmissionRejected,
			"Заявка отклонена",
			context => $"Ваша заявка по задаче \"{context.TaskTitle}\" отклонена.",
			"submission_rejected",
			cancellationToken);
	}

	private async Task NotifySubmissionDecisionAsync(
		Guid submissionId,
		string expectedDecisionStatus,
		string eventName,
		string operationName,
		string title,
		Func<SubmissionDecisionNotificationContext, string> bodyFactory,
		string type,
		CancellationToken cancellationToken)
	{
		await ExecuteNotificationAsync(
			eventName,
			operationName,
			new (string Name, object? Value)[]
			{
				("SubmissionId", submissionId),
				("ExpectedDecisionStatus", expectedDecisionStatus),
			},
			async ct =>
			{
				if(submissionId == Guid.Empty)
				{
					_logger.LogRejected(
						LogLevel.Warning,
						eventName,
						LogLayers.ApplicationService,
						operationName,
						"Submission decision push notification rejected by validation.",
						("SubmissionId", submissionId),
						("ExpectedDecisionStatus", expectedDecisionStatus));
					return;
				}

				var context = await _notificationReadRepository.GetSubmissionDecisionContextAsync(submissionId, ct);
				if(context is null)
				{
					_logger.LogRejected(
						LogLevel.Warning,
						eventName,
						LogLayers.ApplicationService,
						operationName,
						"Submission decision push notification rejected. Submission context not found.",
						("SubmissionId", submissionId),
						("ExpectedDecisionStatus", expectedDecisionStatus));
					return;
				}

				if(!string.Equals(context.DecisionStatus, expectedDecisionStatus, StringComparison.Ordinal))
				{
					_logger.LogRejected(
						LogLevel.Warning,
						eventName,
						LogLayers.ApplicationService,
						operationName,
						"Submission decision push notification rejected. Decision status mismatch.",
						("SubmissionId", context.SubmissionId),
						("DecisionStatus", context.DecisionStatus),
						("ExpectedDecisionStatus", expectedDecisionStatus));
					return;
				}

				var targets = await _pushDevices.GetActiveTargetsByUserIdAsync(context.UserId, ct);
				if(targets.Count == 0)
				{
					_logger.LogCompleted(
						LogLevel.Debug,
						eventName,
						LogLayers.ApplicationService,
						operationName,
						"Submission decision push notification completed. No targets found.",
						("SubmissionId", context.SubmissionId),
						("UserId", context.UserId),
						("TargetCount", 0));
					return;
				}

				var message = new PushMessage(
					Title: title,
					Body: bodyFactory(context),
					Data: new Dictionary<string, string>(StringComparer.Ordinal)
					{
						["type"] = type,
						["taskId"] = context.TaskId.ToString("D"),
						["submissionId"] = context.SubmissionId.ToString("D"),
					});

				var sendResult = await _sender.SendManyAsync(targets, message, ct);
				await DeactivateInvalidTokensAsync(
					sendResult.InvalidTokens,
					eventName,
					operationName,
					ct,
					("SubmissionId", context.SubmissionId),
					("UserId", context.UserId));

				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Submission decision push notification completed.",
					("SubmissionId", context.SubmissionId),
					("UserId", context.UserId),
					("TargetCount", targets.Count),
					("SuccessCount", sendResult.SuccessCount),
					("FailureCount", sendResult.FailureCount),
					("InvalidTokenCount", sendResult.InvalidTokens.Count));
			},
			cancellationToken);
	}

	private async Task DeactivateInvalidTokensAsync(
		IReadOnlyList<string> invalidTokens,
		string parentEventName,
		string parentOperationName,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] parentProperties)
	{
		if(invalidTokens.Count == 0)
		{
			return;
		}

		var properties = StructuredLog.Combine(
			parentProperties,
			("InvalidTokenCount", invalidTokens.Count));

		using var scope = _logger.BeginExecutionScope(
			parentEventName,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.DeactivateInvalidTokens,
			properties);

		_logger.LogStarted(
			parentEventName,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Push.DeactivateInvalidTokens,
			"Invalid push token deactivation started.",
			properties);

		try
		{
			await _pushDevices.DeactivateManyByTokensAsync(
				invalidTokens,
				DateTimeOffset.UtcNow,
				cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				parentEventName,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.DeactivateInvalidTokens,
				"Invalid push token deactivation completed.",
				properties);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				parentEventName,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.DeactivateInvalidTokens,
				"Invalid push token deactivation aborted.",
				properties);
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				parentEventName,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Push.DeactivateInvalidTokens,
				"Invalid push token deactivation failed.",
				ex,
				properties);
		}
	}

	private async Task ExecuteNotificationAsync(
		string eventName,
		string operationName,
		(string Name, object? Value)[] properties,
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken)
	{
		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Push notification orchestration started.",
			properties);

		try
		{
			await action(cancellationToken);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Push notification orchestration aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Push notification orchestration failed.",
				ex,
				properties);
			throw;
		}
	}
}