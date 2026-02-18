using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskSubmissionRepository
{
	Task<TaskOperationResult> ValidateActorAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
	Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> DeleteSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken);
	Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, DateTimeOffset decidedAt, CancellationToken cancellationToken);
	Task<TaskOperationResult> RejectAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, DateTimeOffset decidedAt, CancellationToken cancellationToken);
}