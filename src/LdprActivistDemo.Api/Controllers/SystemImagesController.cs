using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.Helpers;
using LdprActivistDemo.Api.RateLimiting;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Images;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Эндпоинты работы с системными изображениями.
/// </summary>
[ApiController]
[Route("api/v1/system-images")]
public sealed class SystemImagesController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IImageService _images;

	public SystemImagesController(IImageService images)
	{
		_images = images ?? throw new ArgumentNullException(nameof(images));
	}

	/// <summary>
	/// Возвращает бинарное содержимое системного изображения по его имени.
	/// </summary>
	/// <param name="imgName">Имя системного изображения.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Системное изображение найдено и возвращено как файл.</response>
	/// <response code="400">Передано пустое или некорректное имя изображения.</response>
	/// <response code="404">Системное изображение с указанным именем не найдено.</response>
	/// <response code="304">Клиент уже имеет актуальную версию изображения по ETag.</response>
	[HttpGet("{imgName}")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetByNameAsync(
		[FromRoute] string imgName,
		CancellationToken cancellationToken)
	{
		imgName = NormalizeName(imgName);
		if(string.IsNullOrWhiteSpace(imgName))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["name"] = new[] { "Name is required." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте непустое имя системной картинки.");
		}

		var image = await _images.GetSystemByNameAsync(imgName, cancellationToken);
		if(image is null)
		{
			return this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.SystemImageNotFound,
				"Системная картинка не найдена.");
		}

		if(ImageHttpCacheHelper.IsNotModified(Request, Response, image))
		{
			return StatusCode(StatusCodes.Status304NotModified);
		}

		return File(image.Data, image.ContentType);
	}

	/// <summary>
	/// Создаёт или обновляет системное изображение по имени.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// Если изображение с таким именем уже существует, выполняется замена файла.
	/// Если не существует — создаётся новая запись.
	/// </remarks>
	/// <param name="imgName">Имя системного изображения.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Multipart/form-data с файлом изображения.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="201">Системное изображение создано.</response>
	/// <response code="200">Системное изображение обновлено.</response>
	/// <response code="400">Переданы некорректные данные запроса или неподдерживаемый файл.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Операция запрещена для текущей роли.</response>
	[HttpPut("{imgName}")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(SystemImageDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(SystemImageDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> UpsertAsync(
		   [FromRoute] string imgName,
		   [FromQuery] Guid actorUserId,
		   [FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		   [FromForm] UpsertSystemImageFormRequest request,
		   CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		imgName = NormalizeName(imgName);

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(string.IsNullOrWhiteSpace(imgName))
		{
			errors["name"] = new[] { "Name is required." };
		}

		if(request.Image is null)
		{
			errors["image"] = new[] { "Image is required." };
		}
		else
		{
			var imageError = UploadedImageReader.ValidateImage(request.Image);
			if(imageError is not null)
			{
				errors["image"] = new[] { imageError };
			}
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте имя и файл системной картинки.");
		}

		var image = await UploadedImageReader.ReadAsync(request.Image!, cancellationToken);
		var result = await _images.UpsertSystemImageAsync(actorUserId, actorUserPassword!, imgName, image, cancellationToken);

		if(!result.IsSuccess)
		{
			return result.Error switch
			{
				LdprActivistDemo.Application.Images.Models.SystemImageUpsertError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				"Проверьте имя и файл системной картинки."),
				LdprActivistDemo.Application.Images.Models.SystemImageUpsertError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),
				LdprActivistDemo.Application.Images.Models.SystemImageUpsertError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Операция доступна только пользователю с ролью admin."),
				_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
			}
	 ;
		}

		var dto = new SystemImageDto(result.Value!.Id, result.Value.ImageId, result.Value.Name);
		return result.IsCreated
			? Created($"/api/v1/system-images/{Uri.EscapeDataString(dto.Name)}", dto)
			: Ok(dto);
	}

	/// <summary>
	/// Модель multipart/form-data для создания или обновления системного изображения.
	/// </summary>
	public sealed class UpsertSystemImageFormRequest
	{
		/// <summary>
		/// Файл изображения.
		/// </summary>
		public IFormFile? Image { get; set; }
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim().ToLowerInvariant();
}