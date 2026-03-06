using System.Text.Json;

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
		var dto = regions.Select(x => new RegionDto(x.Name)).ToList();
		return Ok(dto);
	}

	[HttpGet("{regionName}/cities")]
	[ProducesResponseType(typeof(IReadOnlyList<CityDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<CityDto>>> GetCitiesByRegion(string regionName, CancellationToken cancellationToken)
	{
		var cities = await _geo.GetCitiesByRegionAsync(regionName, cancellationToken);
		var dto = cities.Select(x => new CityDto(x.Name)).ToList();
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

		var name = result.Value!.Name;
		return Created($"/api/v1/regions/{Uri.EscapeDataString(name)}", new CreateRegionResponse(name));
	}

	[HttpPost("{regionName}/cities")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CreateCitiesResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<ActionResult<CreateCitiesResponse>> CreateCities(
		string regionName,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] JsonElement request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		if(!TryExtractCityNames(request, out var cityNames) || cityNames.Count == 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["body"] = new[] { "Request must contain at least one city name." },
				},
				title: "Некорректный запрос.",
			   detail: "Передайте массив названий городов либо как корневой JSON-массив, либо в поле names.");
		}

		var result = await _geo.CreateCitiesAsync(
			actorUserId,
			actorUserPassword!,
			regionName,
			cityNames,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapError(result.Error);
		}

		var createdNames = result.Value!.Select(x => x.Name).ToList();
		return Created(
			$"/api/v1/regions/{Uri.EscapeDataString(regionName)}/cities",
			new CreateCitiesResponse(createdNames));
	}

	[HttpPut("{regionName}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UpdateRegion(
		string regionName,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] UpdateRegionRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.UpdateRegionAsync(
			actorUserId,
			actorUserPassword!,
			new RegionUpdateModel(regionName, request.Name),
			cancellationToken);

		return result.IsSuccess ? NoContent() : MapError(result.Error);
	}

	[HttpDelete("{regionName}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> DeleteRegion(
		string regionName,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.DeleteRegionAsync(
			actorUserId,
			actorUserPassword!,
			regionName,
			cancellationToken);

		return result.IsSuccess ? NoContent() : MapError(result.Error);
	}

	[HttpPut("{regionName}/cities/{cityName}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UpdateCity(
		string regionName,
		string cityName,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] UpdateCityRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.UpdateCityAsync(
			actorUserId,
			actorUserPassword!,
			new CityUpdateModel(regionName, cityName, request.Name),
			cancellationToken);

		return result.IsSuccess ? NoContent() : MapError(result.Error);
	}

	[HttpDelete("{regionName}/cities/{cityName}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> DeleteCity(
		string regionName,
		string cityName,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.DeleteCityAsync(
			actorUserId,
			actorUserPassword!,
			regionName,
			cityName,
			cancellationToken);

		return result.IsSuccess ? NoContent() : MapError(result.Error);
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

	private static bool TryExtractCityNames(JsonElement request, out List<string> names)
	{
		names = new List<string>();

		switch(request.ValueKind)
		{
			case JsonValueKind.Array:
				AddNamesFromArray(request, names);
				return true;

			case JsonValueKind.Object:
				foreach(var property in request.EnumerateObject())
				{
					if(!string.Equals(property.Name, "names", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					switch(property.Value.ValueKind)
					{
						case JsonValueKind.Array:
							AddNamesFromArray(property.Value, names);
							break;

						case JsonValueKind.String:
							var singleName = property.Value.GetString();
							if(!string.IsNullOrWhiteSpace(singleName))
							{
								names.Add(singleName);
							}
							break;
					}
				}

				return true;

			default:
				return false;
		}
	}

	private static void AddNamesFromArray(JsonElement arrayElement, List<string> names)
	{
		foreach(var item in arrayElement.EnumerateArray())
		{
			if(item.ValueKind != JsonValueKind.String)
			{
				continue;
			}

			var value = item.GetString();
			if(!string.IsNullOrWhiteSpace(value))
			{
				names.Add(value);
			}
		}
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
			GeoMutationError.CityNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.GeoCityNotFound,
				"Город не найден."),
			GeoMutationError.Duplicate => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.GeoDuplicate,
				"Конфликт уникальности.",
				"Объект с таким именем уже существует."),
			GeoMutationError.InUse => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.GeoInUse,
				"Объект используется.",
				"Нельзя удалить объект, пока на него ссылаются другие данные."),
			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
	}
}