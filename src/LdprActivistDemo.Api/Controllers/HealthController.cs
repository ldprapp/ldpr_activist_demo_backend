using LdprActivistDemo.Api.Health;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Служебные эндпоинты состояния и версии бэкенда.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class HealthController : ControllerBase
{
	private readonly IBackendVersionProvider _versionProvider;

	public HealthController(IBackendVersionProvider versionProvider)
	{
		_versionProvider = versionProvider;
	}

	/// <summary>
	/// Проверяет доступность API и возвращает краткий статус сервиса вместе с версией бэкенда.
	/// </summary>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Сервис доступен. В теле возвращаются поля <c>status</c> и <c>version</c>.</response>
	[HttpGet("health")]
	[ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
	public ActionResult<HealthResponse> GetHealth(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Ok(new HealthResponse("ok", _versionProvider.Version));
	}

	/// <summary>
	/// Возвращает текущую строку версии бэкенда без дополнительной служебной информации.
	/// </summary>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Версия бэкенда успешно возвращена.</response>
	[HttpGet("version")]
	[ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
	public ActionResult<string> GetVersion(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Ok(_versionProvider.Version);
	}

	/// <summary>
	/// Ответ эндпоинта проверки состояния сервиса.
	/// </summary>
	public sealed record HealthResponse(string Status, string Version);
}