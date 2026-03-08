using LdprActivistDemo.Application.Images.Models;
using LdprActivistDemo.Application.Users;

namespace LdprActivistDemo.Application.Images;

public sealed class ImageService : IImageService
{
	private readonly IImageRepository _images;
	private readonly IActorAccessService _actorAccess;

	public ImageService(IImageRepository images, IActorAccessService actorAccess)
	{
		_images = images ?? throw new ArgumentNullException(nameof(images));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
	}

	public Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default)
		=> _images.GetAsync(id, cancellationToken);

	public async Task<ImageDeleteResult> DeleteAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid id,
		CancellationToken cancellationToken = default)
	{
		if(actorUserId == Guid.Empty || id == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return ImageDeleteResult.Fail(ImageDeleteError.ValidationFailed);
		}

		var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!auth.IsSuccess)
		{
			return ImageDeleteResult.Fail(ImageDeleteError.InvalidCredentials);
		}

		var ownerUserId = await _images.GetOwnerUserIdAsync(id, cancellationToken);
		if(ownerUserId is null)
		{
			return ImageDeleteResult.Fail(ImageDeleteError.ImageNotFound);
		}

		if(ownerUserId.Value != actorUserId)
		{
			return ImageDeleteResult.Fail(ImageDeleteError.Forbidden);
		}

		var deleted = await _images.DeleteAsync(id, cancellationToken);
		return deleted
			? ImageDeleteResult.Success()
			: ImageDeleteResult.Fail(ImageDeleteError.ImageNotFound);
	}

	public Task<Guid> CreateAsync(Guid ownerUserId, ImageCreateModel model, CancellationToken cancellationToken = default)
		=> _images.CreateAsync(ownerUserId, model, cancellationToken);

	public Task<IReadOnlyList<Guid>> CreateManyAsync(
		Guid ownerUserId,
		IReadOnlyList<ImageCreateModel> models,
		CancellationToken cancellationToken = default)
		=> _images.CreateManyAsync(ownerUserId, models, cancellationToken);
}