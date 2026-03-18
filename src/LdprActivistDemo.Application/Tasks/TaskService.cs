using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks;

public sealed class TaskService : ITaskService
{
	private readonly ITaskRepository _tasks;
	private readonly ITaskSubmissionRepository _submissions;
	private readonly IActorAccessService _actorAccess;

	public TaskService(ITaskRepository tasks, ITaskSubmissionRepository submissions, IActorAccessService actorAccess)
	{
		_tasks = tasks;
		_submissions = submissions;
		_actorAccess = actorAccess;
	}

	public async Task<TaskOperationResult> ValidateActorAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? TaskOperationResult.Success()
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, string actorUserPassword, TaskCreateModel model, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _tasks.CreateAsync(actorUserId, model, cancellationToken)
			: TaskOperationResult<Guid>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> UpdateAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskUpdateModel model, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _tasks.UpdateAsync(actorUserId, taskId, model, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> DeleteAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _tasks.DeleteAsync(actorUserId, taskId, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> OpenAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _tasks.OpenAsync(actorUserId, taskId, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> CloseAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _tasks.CloseAsync(actorUserId, taskId, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}
	public async Task<TaskOperationResult<TaskModel>> GetCoordinatorAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null ? await _tasks.GetCoordinatorAsync(actorUserId, taskId, cancellationToken) : TaskOperationResult<TaskModel>.Fail(authError.Value);
	}

	public Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken)
		=> _tasks.GetPublicAsync(taskId, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetByRegionAndSettlementAsync(string regionName, string settlementName, CancellationToken cancellationToken)
		=> _tasks.GetByRegionAndSettlementAsync(regionName, settlementName, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken)
		=> _tasks.GetByRegionAsync(regionName, cancellationToken);

	public Task<IReadOnlyList<TaskModel>> GetBySettlementAsync(string settlementName, CancellationToken cancellationToken)
		=> _tasks.GetBySettlementAsync(settlementName, cancellationToken); public Task<IReadOnlyList<TaskModel>> GetByCoordinatorAsync(Guid coordinatorUserId, CancellationToken cancellationToken)
		=> _tasks.GetByCoordinatorAsync(coordinatorUserId, cancellationToken);

	public Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetAvailableForUserAsync(Guid userId, CancellationToken cancellationToken)
		=> _tasks.GetAvailableForUserAsync(userId, cancellationToken);

	public async Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid userId, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty || actorUserId != userId)
		{
			return TaskSubmitOperationResult.Fail(TaskOperationError.ValidationFailed);
		}

		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.SubmitAsync(actorUserId, userId, taskId, model, cancellationToken)
			: TaskSubmitOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> SubmitForReviewAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.SubmitForReviewAsync(actorUserId, submissionId, model, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.UpdateSubmissionAsync(actorUserId, submissionId, model, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> DeleteSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.DeleteSubmissionAsync(actorUserId, taskId, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.GetSubmittedUsersAsync(actorUserId, taskId, cancellationToken)
			: TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.GetApprovedUsersAsync(actorUserId, taskId, cancellationToken)
			: TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorPassword, cancellationToken);
		return authError is null
			? await _submissions.GetSubmittedUserAsync(actorUserId, taskId, userId, cancellationToken)
			: TaskOperationResult<SubmissionUserViewModel>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorPassword, cancellationToken);
		return authError is null
			? await _submissions.ApproveAsync(actorUserId, submissionId, DateTimeOffset.UtcNow, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult> RejectAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorPassword, cancellationToken);
		return authError is null
			? await _submissions.RejectAsync(actorUserId, submissionId, DateTimeOffset.UtcNow, cancellationToken)
			: TaskOperationResult.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _tasks.GetByUserSubmittedAsync(actorUserId, cancellationToken)
			: TaskOperationResult<IReadOnlyList<TaskModel>>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _tasks.GetByUserApprovedAsync(actorUserId, cancellationToken)
			: TaskOperationResult<IReadOnlyList<TaskModel>>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionCoordinatorFeedAsync(
	Guid actorUserId,
	string actorUserPassword,
	Guid? taskId,
	Guid? userId,
	string? decisionStatus,
	CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null ? await _submissions.GetCoordinatorFeedAsync(actorUserId, taskId, userId, decisionStatus, cancellationToken) : TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionUserFeedAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid? taskId,
		Guid userId,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty || actorUserId != userId)
		{
			return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.ValidationFailed);
		}

		if(taskId.HasValue && taskId.Value == Guid.Empty)
		{
			return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.ValidationFailed);
		}

		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.GetUserFeedAsync(taskId, userId, decisionStatus, cancellationToken)
			: TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(authError.Value);
	}

	public async Task<TaskOperationResult<TaskSubmissionModel>> GetSubmissionByIdAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, CancellationToken cancellationToken)
	{
		var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
		return authError is null
			? await _submissions.GetByIdAsync(actorUserId, submissionId, cancellationToken)
			: TaskOperationResult<TaskSubmissionModel>.Fail(authError.Value);
	}

	private async Task<TaskOperationError?> TryAuthenticateActorAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(auth.IsSuccess)
		{
			return null;
		}

		return auth.Error switch
		{
			ActorAuthenticationError.ValidationFailed => TaskOperationError.ValidationFailed,
			_ => TaskOperationError.InvalidCredentials,
		};
	}
}