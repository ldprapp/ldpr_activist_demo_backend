using System.Text.Json;

using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.RateLimiting;
using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Geo;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Эндпоинты справочника регионов и населённых пунктов.
/// </summary>
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

	/// <summary>
	/// Возвращает список всех регионов справочника.
	/// </summary>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Список регионов успешно возвращён.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<RegionDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<RegionDto>>> GetRegions(CancellationToken cancellationToken)
	{
		var regions = await _geo.GetRegionsAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		var dto = regions.Select(x => new RegionDto(x.Name, x.IsDeleted)).ToList();
		return Ok(dto);
	}

	/// <summary>
	/// Возвращает список населённых пунктов для указанного региона.
	/// </summary>
	/// <param name="regionName">Название региона.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Список населённых пунктов успешно возвращён.</response>
	[HttpGet("{regionName}/settlements")]
	[ProducesResponseType(typeof(IReadOnlyList<SettlementDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<SettlementDto>>> GetSettlementsByRegion(string regionName, CancellationToken cancellationToken)
	{
		var settlements = await _geo.GetSettlementsByRegionAsync(regionName, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		var dto = settlements.Select(x => new SettlementDto(x.Name, x.IsDeleted)).ToList();
		return Ok(dto);
	}

	/// <summary>
	/// Создаёт новый регион в справочнике.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Тело запроса с именем нового региона.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="201">Регион успешно создан.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="409">Регион с таким именем уже существует.</response>
	[HttpPost]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
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

	/// <summary>
	/// Создаёт один или несколько населённых пунктов в указанном регионе.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// В теле запроса допускаются два формата:
	/// 1) корневой JSON-массив строк;
	/// 2) JSON-объект с полем <c>names</c>, содержащим массив строк или одну строку.
	/// </remarks>
	/// <param name="regionName">Название региона, в котором создаются населённые пункты.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">JSON-тело запроса со списком названий населённых пунктов.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="201">Населённые пункты успешно созданы.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Указанный регион не найден.</response>
	/// <response code="409">Обнаружен конфликт уникальности или операция запрещена текущим состоянием справочника.</response>
	[HttpPost("{regionName}/settlements")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CreateSettlementsResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<ActionResult<CreateSettlementsResponse>> CreateSettlements(
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

		if(!TryExtractSettlementNames(request, cancellationToken, out var settlementNames) || settlementNames.Count == 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["body"] = new[] { "Request must contain at least one settlement name." },
				},
 				title: "Некорректный запрос.",
			   detail: "Передайте массив названий населённых пунктов либо как корневой JSON-массив, либо в поле names.");
		}

		var result = await _geo.CreateSettlementsAsync(
 			actorUserId,
 			actorUserPassword!,
 			regionName,
			settlementNames,
 			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapError(result.Error);
		}

		var createdNames = result.Value!.Select(x => x.Name).ToList();
		return Created(
$"/api/v1/regions/{Uri.EscapeDataString(regionName)}/settlements",
new CreateSettlementsResponse(createdNames));
	}

	/// <summary>
	/// Переименовывает регион.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	/// <param name="regionName">Текущее название региона.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Тело запроса с новым названием региона.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Регион успешно обновлён.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Регион не найден.</response>
	/// <response code="409">Регион с новым именем уже существует.</response>
	[HttpPut("{regionName}")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
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

	/// <summary>
	/// Помечает регион как удалённый.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// При передаче <c>targetRegionName</c> регион используется как цель миграции задач,
	/// которые были привязаны к удаляемому региону без конкретного населённого пункта.
	/// </remarks>
	/// <param name="regionName">Название удаляемого региона.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="targetRegionName">Опциональное название региона-назначения для миграции связанных данных.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Регион успешно помечен как удалённый.</response>
	/// <response code="400">Переданы некорректные параметры удаления.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Исходный или целевой регион не найден.</response>
	/// <response code="409">Регион нельзя удалить из-за конфликтующего состояния данных.</response>
	[HttpDelete("{regionName}")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> DeleteRegion(
		string regionName,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] string? targetRegionName,
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
			new RegionDeleteModel(regionName, targetRegionName),
			cancellationToken);

		return result.IsSuccess ? NoContent() : MapError(result.Error);
	}

	/// <summary>
	/// Восстанавливает ранее удалённый регион.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	/// <param name="regionName">Название региона для восстановления.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Регион успешно восстановлен.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Регион не найден.</response>
	/// <response code="409">Операция запрещена текущим состоянием данных.</response>
	[HttpPost("{regionName}/restore")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> RestoreRegion(
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

		var result = await _geo.RestoreRegionAsync(actorUserId, actorUserPassword!, regionName, cancellationToken);
		return result.IsSuccess ? NoContent() : MapError(result.Error);
	}

	/// <summary>
	/// Переименовывает населённый пункт внутри указанного региона.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	/// <param name="regionName">Название региона, в котором находится населённый пункт.</param>
	/// <param name="settlementName">Текущее название населённого пункта.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Тело запроса с новым названием населённого пункта.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Населённый пункт успешно обновлён.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Регион или населённый пункт не найден.</response>
	/// <response code="409">Населённый пункт с новым именем уже существует.</response>
	[HttpPut("{regionName}/settlements/{settlementName}")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UpdateSettlement(
 		string regionName,
		string settlementName,
 		[FromQuery] Guid actorUserId,
 		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] UpdateSettlementRequest request,
 		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.UpdateSettlementAsync(
			actorUserId,
			actorUserPassword!,
			new SettlementUpdateModel(regionName, settlementName, request.Name),
			cancellationToken);

		return result.IsSuccess ? NoContent() : MapError(result.Error);
	}

	/// <summary>
	/// Помечает населённый пункт как удалённый.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// Для миграции связанных пользователей и задач можно указать одновременно
	/// <c>targetRegionName</c> и <c>targetSettlementName</c>.
	/// </remarks>
	/// <param name="regionName">Название региона, в котором находится удаляемый населённый пункт.</param>
	/// <param name="settlementName">Название удаляемого населённого пункта.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="targetRegionName">Опциональное название целевого региона для миграции данных.</param>
	/// <param name="targetSettlementName">Опциональное название целевого населённого пункта для миграции данных.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Населённый пункт успешно помечен как удалённый.</response>
	/// <response code="400">Переданы некорректные параметры удаления.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Регион, исходный или целевой населённый пункт не найден.</response>
	/// <response code="409">Операция запрещена текущим состоянием данных.</response>
	[HttpDelete("{regionName}/settlements/{settlementName}")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> DeleteSettlement(
 		string regionName,
		string settlementName,
 		[FromQuery] Guid actorUserId,
 		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
 		[FromQuery] string? targetRegionName,
		[FromQuery] string? targetSettlementName,
 		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}
		var result = await _geo.DeleteSettlementAsync(
			actorUserId,
			actorUserPassword!,
			new SettlementDeleteModel(regionName, settlementName, targetRegionName, targetSettlementName),
			cancellationToken);

		return result.IsSuccess ? NoContent() : MapError(result.Error);
	}

	/// <summary>
	/// Восстанавливает ранее удалённый населённый пункт.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// Регион-родитель должен быть активным.
	/// </remarks>
	/// <param name="regionName">Название региона, в котором находится населённый пункт.</param>
	/// <param name="settlementName">Название населённого пункта для восстановления.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Населённый пункт успешно восстановлен.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Регион или населённый пункт не найден.</response>
	/// <response code="409">Операция запрещена текущим состоянием данных.</response>
	[HttpPost("{regionName}/settlements/{settlementName}/restore")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> RestoreSettlement(
 		string regionName,
		string settlementName,
 		[FromQuery] Guid actorUserId,
 		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
 		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _geo.RestoreSettlementAsync(actorUserId, actorUserPassword!, regionName, settlementName, cancellationToken);
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

	private static bool TryExtractSettlementNames(JsonElement request, CancellationToken cancellationToken, out List<string> names)
	{
		names = new List<string>();

		switch(request.ValueKind)
		{
			case JsonValueKind.Array:
				AddNamesFromArray(request, names, cancellationToken);
				return true;

			case JsonValueKind.Object:
				foreach(var property in request.EnumerateObject())
				{
					cancellationToken.ThrowIfCancellationRequested();

					if(!string.Equals(property.Name, "names", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					switch(property.Value.ValueKind)
					{
						case JsonValueKind.Array:
							AddNamesFromArray(property.Value, names, cancellationToken);
							break;

						case JsonValueKind.String:
							cancellationToken.ThrowIfCancellationRequested();

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

	private static void AddNamesFromArray(JsonElement arrayElement, List<string> names, CancellationToken cancellationToken)
	{
		foreach(var item in arrayElement.EnumerateArray())
		{
			cancellationToken.ThrowIfCancellationRequested();

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
				"Неверные учётные данные или у пользователя нет роли admin."),
			GeoMutationError.RegionNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.GeoRegionNotFound,
				"Регион не найден."),
			GeoMutationError.SettlementNotFound => this.ProblemWithCode(
 				StatusCodes.Status404NotFound,
				ApiErrorCodes.GeoSettlementNotFound,
				"Населённый пункт не найден."),
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
			GeoMutationError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				"Проверьте параметры миграции. Для населённого пункта targetRegionName и targetSettlementName должны быть указаны вместе, а целевой объект не должен совпадать с удаляемым."),
			GeoMutationError.HasActiveSettlements => this.ProblemWithCode(
 				StatusCodes.Status409Conflict,
				ApiErrorCodes.GeoHasActiveSettlements,
				"Регион содержит активные населённые пункты.",
				"Нельзя удалить регион, пока в нём остаётся хотя бы один населённый пункт с IsDeleted = false."),
			GeoMutationError.ParentRegionDeleted => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.GeoParentRegionDeleted,
				"Операция запрещена.",
				"Нельзя востановить населённый пункт, пока его регион является удалённым."),
			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
	}
}