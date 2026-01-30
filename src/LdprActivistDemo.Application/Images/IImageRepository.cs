using LdprActivistDemo.Application.Images.Models;

namespace LdprActivistDemo.Application.Images;

public interface IImageRepository
{
	Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Удаляет картинку.
	/// </summary>
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Создаёт картинку и возвращает её идентификатор.
	/// </summary>
	Task<Guid> CreateAsync(ImageCreateModel model, CancellationToken cancellationToken = default);

	/// <summary>
	/// Создаёт несколько картинок и возвращает их идентификаторы в том же порядке.
	/// </summary>
	Task<IReadOnlyList<Guid>> CreateManyAsync(IReadOnlyList<ImageCreateModel> models, CancellationToken cancellationToken = default);
}