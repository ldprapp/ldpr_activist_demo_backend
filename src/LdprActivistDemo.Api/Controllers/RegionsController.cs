using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Geo;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

[ApiController]
[Route("api/v1/regions")]
public sealed class RegionsController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IGeoDirectoryService _geo;

	public RegionsController(IGeoDirectoryService geo)
	{
		_geo = geo;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<RegionDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<RegionDto>>> GetRegions(CancellationToken cancellationToken)
	{
		var regions = await _geo.GetRegionsAsync(cancellationToken);
		var dto = regions.Select(x => new RegionDto(x.Id, x.Name)).ToList();
		return Ok(dto);
	}

	[HttpGet("{regionId:int}/cities")]
	[ProducesResponseType(typeof(IReadOnlyList<CityDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<CityDto>>> GetCitiesByRegion(int regionId, CancellationToken cancellationToken)
	{
		var cities = await _geo.GetCitiesByRegionAsync(regionId, cancellationToken);
		var dto = cities.Select(x => new CityDto(x.Id, x.Name)).ToList();
		return Ok(dto);
	}

	[HttpPost]
	[ProducesResponseType(typeof(CreateRegionResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<ActionResult<CreateRegionResponse>> CreateRegion(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] CreateRegionRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.CreateRegionAsync(
			actorUserId,
			actorUserPassword!,
			new RegionCreateModel(request.Name),
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapError(result.Error);
		}

		var id = result.Value!.Id;
		return Created($"/api/v1/regions/{id}", new CreateRegionResponse(id));
	}

	[HttpPost("{regionId:int}/cities")]
	[ProducesResponseType(typeof(CreateCityResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<ActionResult<CreateCityResponse>> CreateCity(
		int regionId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] CreateCityRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.CreateCityAsync(
			actorUserId,
			actorUserPassword!,
			new CityCreateModel(regionId, request.Name),
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapError(result.Error);
		}

		var id = result.Value!.Id;
		return Created($"/api/v1/regions/{regionId}/cities/{id}", new CreateCityResponse(id));
	}

	private ActionResult? TryBuildActorValidationProblem(Guid actorUserId, string? actorUserPassword)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(actorUserId == Guid.Empty)
		{
			errors["actorUserId"] = new[] { "ActorUserId is required." };
		}

		if(string.IsNullOrWhiteSpace(actorUserPassword))
		{
			errors["actorUserPassword"] = new[] { $"ActorUserPassword is required (use {ActorPasswordHeader} header)." };
		}

		if(errors.Count == 0)
		{
			return null;
		}

		return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
	}

	private ActionResult MapError(GeoMutationError error)
	{
		return error switch
		{
			GeoMutationError.InvalidName => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.GeoInvalidName,
				"Некорректное имя.",
				"Имя не должно быть пустым."),
			GeoMutationError.Unauthorized => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.GeoUnauthorized,
				"Нет доступа.",
				"Неверные учётные данные или пользователь не админ."),
			GeoMutationError.RegionNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.GeoRegionNotFound,
				"Регион не найден."),
			GeoMutationError.Duplicate => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.GeoDuplicate,
				"Конфликт уникальности.",
				"Объект с таким именем уже существует."),
			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
	}
}