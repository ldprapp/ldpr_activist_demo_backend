using LdprActivistDemo.Application.Images.Models;

namespace LdprActivistDemo.Application.Images;

public interface IImageService
{
	Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default);
	Task<ImageDeleteResult> DeleteAsync(Guid actorUserId, string actorUserPassword, Guid id, CancellationToken cancellationToken = default);
	Task<Guid> CreateAsync(Guid ownerUserId, ImageCreateModel model, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<Guid>> CreateManyAsync(Guid ownerUserId, IReadOnlyList<ImageCreateModel> models, CancellationToken cancellationToken = default);
}