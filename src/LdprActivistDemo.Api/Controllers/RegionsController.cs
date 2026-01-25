using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Contracts.Geo;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

[ApiController]
[Route("api/v1/regions")]
public sealed class RegionsController : ControllerBase
{
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
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<ActionResult<CreateRegionResponse>> CreateRegion(
		[FromBody] CreateRegionRequest request,
		CancellationToken cancellationToken)
	{
		var result = await _geo.CreateRegionAsync(
			new RegionCreateModel(request.ActorUserId, request.ActorPasswordHash, request.Name),
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
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<ActionResult<CreateCityResponse>> CreateCity(
		int regionId,
		[FromBody] CreateCityRequest request,
		CancellationToken cancellationToken)
	{
		var result = await _geo.CreateCityAsync(
			new CityCreateModel(request.ActorUserId, request.ActorPasswordHash, regionId, request.Name),
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapError(result.Error);
		}

		var id = result.Value!.Id;
		return Created($"/api/v1/regions/{regionId}/cities/{id}", new CreateCityResponse(id));
	}

	private ActionResult MapError(GeoMutationError error)
	{
		return error switch
		{
			GeoMutationError.InvalidName => BadRequest(),
			GeoMutationError.Unauthorized => Unauthorized(),
			GeoMutationError.RegionNotFound => NotFound(),
			GeoMutationError.Duplicate => Conflict(),
			_ => StatusCode(StatusCodes.Status500InternalServerError),
		};
	}
}