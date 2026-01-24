using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks;

public sealed class TaskService : ITaskService
{
	private readonly ITaskRepository _tasks;
	private readonly ITaskSubmissionRepository _submissions;

	public TaskService(ITaskRepository tasks, ITaskSubmissionRepository submissions)
	{
		_tasks = tasks;
		_submissions = submissions;
	}

	public Task<Guid> CreateAsync(TaskCreateModel model, CancellationToken cancellationToken)
		=> _tasks.CreateAsync(model, cancellationToken);

	public Task<bool> UpdateAsync(TaskUpdateModel model, CancellationToken cancellationToken)
		=> _tasks.UpdateAsync(model, cancellationToken);

	public Task<bool> DeleteAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
		=> _tasks.DeleteAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);

	public Task<bool> CloseAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
		=> _tasks.CloseAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);

	public Task<TaskModel?> GetAsync(Guid taskId, CancellationToken cancellationToken)
		=> _tasks.GetAsync(taskId, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken)
		=> _tasks.GetByRegionAndCityAsync(regionId, cityId, cancellationToken);

	public Task<bool> SubmitAsync(TaskSubmissionCreateModel model, CancellationToken cancellationToken)
		=> _submissions.SubmitAsync(model, cancellationToken);

	public Task<IReadOnlyList<UserFullNameModel>> GetSubmittedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
		=> _submissions.GetSubmittedUsersAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);

	public Task<IReadOnlyList<UserFullNameModel>> GetApprovedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
		=> _submissions.GetApprovedUsersAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);

	public Task<SubmissionUserViewModel?> GetSubmittedUserAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, CancellationToken cancellationToken)
		=> _submissions.GetSubmittedUserAsync(actorUserId, actorPasswordHash, taskId, userId, cancellationToken);

	public Task<bool> ApproveAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, CancellationToken cancellationToken)
		=> _submissions.ApproveAsync(actorUserId, actorPasswordHash, taskId, userId, DateTimeOffset.UtcNow, cancellationToken);
}