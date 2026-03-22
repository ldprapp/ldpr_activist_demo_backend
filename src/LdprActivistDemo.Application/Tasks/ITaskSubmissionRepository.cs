using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskSubmissionRepository
{
	Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, Guid userId, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> SubmitForReviewAsync(Guid actorUserId, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> DeleteSubmissionAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, Guid taskId, Guid userId, CancellationToken cancellationToken);
	Task<TaskOperationResult> ApproveAsync(Guid actorUserId, Guid submissionId, DateTimeOffset decidedAt, CancellationToken cancellationToken);
	Task<TaskOperationResult> RejectAsync(Guid actorUserId, Guid submissionId, DateTimeOffset decidedAt, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetReviewerFeedAsync(
		Guid reviewerUserId,
		Guid? taskId,
		Guid? userId,
		string? decisionStatus,
		CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetExecutorFeedAsync(Guid? taskId, Guid userId, string? decisionStatus, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<Guid>>> GetTaskIdsByUserAndDecisionStatusAsync(
		Guid userId,
		string decisionStatus,
		CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskSubmissionModel>> GetByIdAsync(Guid actorUserId, Guid submissionId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetTaskUsersAsync(
		Guid actorUserId,
		Guid taskId,
		string? decisionStatus,
		CancellationToken cancellationToken);
}