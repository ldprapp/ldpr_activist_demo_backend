using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.Helpers;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Application.Images.Models;
using LdprActivistDemo.Contracts.Errors;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Эндпоинты работы с пользовательскими изображениями.
/// </summary>
[ApiController]
[Route("api/v1/images")]
public sealed class ImagesController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IImageService _images;

	public ImagesController(IImageService images)
	{
		_images = images ?? throw new ArgumentNullException(nameof(images));
	}

	/// <summary>
	/// Возвращает бинарное содержимое пользовательского изображения по его идентификатору.
	/// </summary>
	/// <param name="id">Идентификатор изображения.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Изображение найдено и возвращено как файл.</response>
	/// <response code="400">Передан пустой или некорректный идентификатор изображения.</response>
	/// <response code="404">Изображение с указанным идентификатором не найдено.</response>
	/// <response code="304">Клиент уже имеет актуальную версию изображения по ETag.</response>
	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetAsync(
		[FromRoute] Guid id,
		CancellationToken cancellationToken)
	{
		if(id == Guid.Empty)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["id"] = new[] { "Id is required." },
				},
				title: "Некорректный запрос.",
				detail: "id обязателен.");
		}

		var img = await _images.GetAsync(id, cancellationToken);
		if(img is null)
		{
			return this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.ImageNotFound,
				"Картинка не найдена.");
		}

		if(ImageHttpCacheHelper.IsNotModified(Request, Response, img))
		{
			return StatusCode(StatusCodes.Status304NotModified);
		}

		return File(img.Data, img.ContentType);
	}

	/// <summary>
	/// Удаляет пользовательское изображение.
	/// </summary>
	/// <remarks>
	/// Удаление доступно только владельцу изображения. Системные изображения и изображения,
	/// которые используются как системные, этим эндпоинтом удалить нельзя.
	/// </remarks>
	/// <param name="id">Идентификатор изображения.</param>
	/// <param name="actorUserId">Идентификатор пользователя, от имени которого выполняется операция.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Изображение успешно удалено.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не является владельцем изображения.</response>
	/// <response code="404">Изображение не найдено.</response>
	/// <response code="409">Изображение используется системой и не может быть удалено.</response>
	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> DeleteAsync(
		[FromRoute] Guid id,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}
		if(id == Guid.Empty)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["id"] = new[] { "Id is required." },
				},
				title: "Некорректный запрос.",
				detail: "id обязателен.");
		}

		var result = await _images.DeleteAsync(actorUserId, actorUserPassword!, id, cancellationToken);
		return result.Error switch
		{
			ImageDeleteError.None => NoContent(),
			ImageDeleteError.ValidationFailed => this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["id"] = new[] { "Id is required." },
				},
				title: "Некорректный запрос.",
				detail: "Проверьте id картинки."),
			ImageDeleteError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),
			ImageDeleteError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Удалять картинку может только её владелец."),
			ImageDeleteError.ImageNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.ImageNotFound,
				"Картинка не найдена."),
			ImageDeleteError.InUse => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.ImageInUse,
				"Картинка используется.",
				"Нельзя удалить картинку, пока она используется как системная."),
			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
	}
}