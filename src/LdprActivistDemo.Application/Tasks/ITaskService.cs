using LdprActivistDemo.Application.Tasks.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskService
{
	Task<TaskOperationResult> ValidateActorAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
	Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, string actorUserPassword, TaskCreateModel model, CancellationToken cancellationToken);

	Task<TaskOperationResult> UpdateAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid taskId,
		TaskUpdateModel model,
		CancellationToken cancellationToken);
	Task<TaskOperationResult> DeleteAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult> CloseAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskModel>> GetAdminAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAsync(int regionId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByCityAsync(int cityId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByAdminAsync(Guid adminUserId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetAvailableForUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<TaskOperationResult> DeleteSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<LdprActivistDemo.Application.Users.Models.UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<LdprActivistDemo.Application.Users.Models.UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken);
	Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken);
	Task<TaskOperationResult> RejectAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
}