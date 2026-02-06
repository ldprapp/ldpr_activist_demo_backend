namespace LdprActivistDemo.Application.Tasks;

public interface ITaskFeedRepository
{
	Task<IReadOnlyList<Guid>> GetAllTaskIdsAsync(CancellationToken cancellationToken);
}