using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskService
{
	Task<TaskOperationResult> ValidateActorAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
	Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, string actorUserPassword, TaskCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> UpdateAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskUpdateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> OpenAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult> CloseAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskModel>> GetCoordinatorAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAndSettlementAsync(string regionName, string settlementName, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetBySettlementAsync(string settlementName, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByCoordinatorAsync(Guid coordinatorUserId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetAvailableForUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid userId, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> SubmitForReviewAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> DeleteSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<LdprActivistDemo.Application.Users.Models.UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<LdprActivistDemo.Application.Users.Models.UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken); Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken);
	Task<TaskOperationResult> RejectAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionReviewerFeedAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid reviewerUserId,
		Guid? taskId,
		Guid? userId,
		string? decisionStatus,
		CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionExecutorFeedAsync(Guid actorUserId, string actorUserPassword, Guid? taskId, Guid userId, string? decisionStatus, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<Guid>>> GetTaskIdsByUserSubmissionStatusAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		string decisionStatus,
		CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskSubmissionModel>> GetSubmissionByIdAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetTaskUsersAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid taskId,
		string? taskStatus,
		CancellationToken cancellationToken);
}