using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks;

public interface ITaskSubmissionRepository
{
	Task<bool> SubmitAsync(TaskSubmissionCreateModel model, CancellationToken cancellationToken);

	Task<IReadOnlyList<UserFullNameModel>> GetSubmittedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetApprovedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken);
	Task<SubmissionUserViewModel?> GetSubmittedUserAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, CancellationToken cancellationToken);
	Task<bool> ApproveAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, DateTimeOffset confirmedAt, CancellationToken cancellationToken);
}