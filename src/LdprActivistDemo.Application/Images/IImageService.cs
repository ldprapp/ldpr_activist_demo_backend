using LdprActivistDemo.Application.Images.Models;

namespace LdprActivistDemo.Application.Images;

public interface IImageService
{
	Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

	Task<Guid> CreateAsync(ImageCreateModel model, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Guid>> CreateManyAsync(
		IReadOnlyList<ImageCreateModel> models,
		CancellationToken cancellationToken = default);
}