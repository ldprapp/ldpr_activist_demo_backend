using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskService
{
	Task<Guid> CreateAsync(TaskCreateModel model, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(TaskUpdateModel model, CancellationToken cancellationToken);
	Task<bool> DeleteAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);
	Task<bool> CloseAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);

	Task<TaskModel?> GetAsync(Guid taskId, CancellationToken cancellationToken);
	Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken);

	Task<bool> SubmitAsync(TaskSubmissionCreateModel model, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetSubmittedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetApprovedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);
	Task<SubmissionUserViewModel?> GetSubmittedUserAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, CancellationToken cancellationToken);
	Task<bool> ApproveAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, CancellationToken cancellationToken);
}