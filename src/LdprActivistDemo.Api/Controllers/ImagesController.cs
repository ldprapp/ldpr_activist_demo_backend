using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Contracts.Errors;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

[ApiController]
[Route("api/v1/images")]
public sealed class ImagesController : ControllerBase
{
	private readonly IImageService _images;

	public ImagesController(IImageService images)
	{
		_images = images ?? throw new ArgumentNullException(nameof(images));
	}

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetAsync([FromRoute] Guid id, CancellationToken cancellationToken)
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

		return File(img.Data, img.ContentType);
	}

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
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

		var ok = await _images.DeleteAsync(id, cancellationToken);
		return ok
			? NoContent()
			: this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.ImageNotFound, "Картинка не найдена.");
	}
}