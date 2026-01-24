using LdprActivistDemo.Application.Tasks.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskRepository
{
	Task<Guid> CreateAsync(TaskCreateModel model, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(TaskUpdateModel model, CancellationToken cancellationToken);
	Task<bool> DeleteAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);
	Task<bool> CloseAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);

	Task<TaskModel?> GetAsync(Guid taskId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken);
}