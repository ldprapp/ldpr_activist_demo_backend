using LdprActivistDemo.Application.Images.Models;

namespace LdprActivistDemo.Application.Images;

public sealed class ImageService : IImageService
{
	private readonly IImageRepository _images;

	public ImageService(IImageRepository images)
	{
		_images = images ?? throw new ArgumentNullException(nameof(images));
	}

	public Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default)
		=> _images.GetAsync(id, cancellationToken);

	public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
		=> _images.DeleteAsync(id, cancellationToken);

	public Task<Guid> CreateAsync(ImageCreateModel model, CancellationToken cancellationToken = default)
		=> _images.CreateAsync(model, cancellationToken);

	public Task<IReadOnlyList<Guid>> CreateManyAsync(
		IReadOnlyList<ImageCreateModel> models,
		CancellationToken cancellationToken = default)
		=> _images.CreateManyAsync(models, cancellationToken);
}