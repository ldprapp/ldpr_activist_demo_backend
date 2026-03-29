namespace LdprActivistDemo.Application.Push;

public interface ITaskPushNotificationService
{
	Task NotifyTaskCreatedAsync(
		Guid taskId,
		CancellationToken cancellationToken);

	Task NotifySubmissionApprovedAsync(
		Guid submissionId,
		CancellationToken cancellationToken);

	Task NotifySubmissionRejectedAsync(
		Guid submissionId,
		CancellationToken cancellationToken);
}