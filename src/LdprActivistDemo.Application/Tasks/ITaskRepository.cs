using LdprActivistDemo.Application.Tasks.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskRepository
{
	Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, string actorUserPassword, TaskCreateModel model, CancellationToken cancellationToken);

	Task<TaskOperationResult> UpdateAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid taskId,
		TaskUpdateModel model,
		CancellationToken cancellationToken);

	Task<TaskOperationResult> DeleteAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult> OpenAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult> CloseAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskModel>> GetAdminAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken);
	Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(string regionName, string cityName, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByCityAsync(string cityName, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByAdminAsync(Guid adminUserId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetAvailableForUserAsync(Guid userId, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
	Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken);
}