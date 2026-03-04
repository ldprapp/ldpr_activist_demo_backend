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

	public Task<TaskOperationResult> ValidateActorAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
		=> _submissions.ValidateActorAsync(actorUserId, actorUserPassword, cancellationToken);

	public Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, string actorUserPassword, TaskCreateModel model, CancellationToken cancellationToken)
		=> _tasks.CreateAsync(actorUserId, actorUserPassword, model, cancellationToken);

	public Task<TaskOperationResult> UpdateAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskUpdateModel model, CancellationToken cancellationToken)
		=> _tasks.UpdateAsync(actorUserId, actorUserPassword, taskId, model, cancellationToken);

	public Task<TaskOperationResult> DeleteAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
		=> _tasks.DeleteAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

	public Task<TaskOperationResult> OpenAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
		=> _tasks.OpenAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

	public Task<TaskOperationResult> CloseAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
		=> _tasks.CloseAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

	public Task<TaskOperationResult<TaskModel>> GetAdminAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
		=> _tasks.GetAdminAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

	public Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken)
		=> _tasks.GetPublicAsync(taskId, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken)
		=> _tasks.GetByRegionAndCityAsync(regionId, cityId, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetByRegionAsync(int regionId, CancellationToken cancellationToken)
		=> _tasks.GetByRegionAsync(regionId, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetByCityAsync(int cityId, CancellationToken cancellationToken)
		=> _tasks.GetByCityAsync(cityId, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetByAdminAsync(Guid adminUserId, CancellationToken cancellationToken)
		=> _tasks.GetByAdminAsync(adminUserId, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetAvailableForUserAsync(Guid userId, CancellationToken cancellationToken)
		=> _tasks.GetAvailableForUserAsync(userId, cancellationToken);

	public Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
		=> _submissions.SubmitAsync(actorUserId, actorUserPassword, taskId, model, cancellationToken);

	public Task<TaskOperationResult> SubmitForReviewAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
		=> _submissions.SubmitForReviewAsync(actorUserId, actorUserPassword, submissionId, model, cancellationToken);

	public Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
		=> _submissions.UpdateSubmissionAsync(actorUserId, actorUserPassword, submissionId, model, cancellationToken);

	public Task<TaskOperationResult> DeleteSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
		=> _submissions.DeleteSubmissionAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
		=> _submissions.GetSubmittedUsersAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
		=> _submissions.GetApprovedUsersAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

	public Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken)
		=> _submissions.GetSubmittedUserAsync(actorUserId, actorPassword, taskId, userId, cancellationToken);

	public Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken)
		=> _submissions.ApproveAsync(actorUserId, actorPassword, submissionId, DateTimeOffset.UtcNow, cancellationToken);

	public Task<TaskOperationResult> RejectAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken)
		=> _submissions.RejectAsync(actorUserId, actorPassword, submissionId, DateTimeOffset.UtcNow, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
		=> _tasks.GetByUserSubmittedAsync(actorUserId, actorUserPassword, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
		=> _tasks.GetByUserApprovedAsync(actorUserId, actorUserPassword, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionAdminFeedAsync(Guid actorUserId, string actorUserPassword, Guid? taskId, Guid? userId, string? decisionStatus, CancellationToken cancellationToken)
		=> _submissions.GetAdminFeedAsync(actorUserId, actorUserPassword, taskId, userId, decisionStatus, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionUserFeedAsync(Guid actorUserId, string actorUserPassword, string? decisionStatus, CancellationToken cancellationToken)
		=> _submissions.GetUserFeedAsync(actorUserId, actorUserPassword, decisionStatus, cancellationToken);

	public Task<TaskOperationResult<TaskSubmissionModel>> GetSubmissionByIdAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, CancellationToken cancellationToken)
		=> _submissions.GetByIdAsync(actorUserId, actorUserPassword, submissionId, cancellationToken);
}