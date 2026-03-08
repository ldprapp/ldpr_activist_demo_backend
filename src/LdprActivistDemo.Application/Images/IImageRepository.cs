using LdprActivistDemo.Application.Images.Models;

namespace LdprActivistDemo.Application.Images;

public interface IImageRepository
{
	Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Guid?> GetOwnerUserIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Guid> CreateAsync(Guid ownerUserId, ImageCreateModel model, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<Guid>> CreateManyAsync(Guid ownerUserId, IReadOnlyList<ImageCreateModel> models, CancellationToken cancellationToken = default);
}