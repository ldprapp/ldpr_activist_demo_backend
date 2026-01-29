using LdprActivistDemo.Api.Health;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class HealthController : ControllerBase
{
	private readonly IBackendVersionProvider _versionProvider;

	public HealthController(IBackendVersionProvider versionProvider)
	{
		_versionProvider = versionProvider;
	}

	/// <summary>Проверка доступности сервиса с выдачей версии бэкенда.</summary>
	[HttpGet("health")]
	[ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
	public ActionResult<HealthResponse> GetHealth()
	{
		return Ok(new HealthResponse("ok", _versionProvider.Version));
	}

	/// <summary>Возвращает строку версии бэкенда.</summary>
	[HttpGet("version")]
	[ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
	public ActionResult<string> GetVersion()
	{
		return Ok(_versionProvider.Version);
	}

	public sealed record HealthResponse(string Status, string Version);
}