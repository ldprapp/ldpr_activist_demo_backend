using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.Time;
using LdprActivistDemo.Application.UserRatings;
using LdprActivistDemo.Application.UserRatings.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.UserRatings;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Отдельная группа эндпоинтов для пользовательских рейтингов.
/// </summary>
[ApiController]
[Route("api/v1/user-ratings")]
public sealed class UserRatingsController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IUserRatingsService _ratings;
	private readonly IUserRatingsRefreshAdminService _refreshAdmin;
	private readonly ApplicationTimeOptions _applicationTimeOptions;

	public UserRatingsController(
		IUserRatingsService ratings,
		IUserRatingsRefreshAdminService refreshAdmin,
		IOptions<ApplicationTimeOptions> applicationTimeOptions)
	{
		_ratings = ratings ?? throw new ArgumentNullException(nameof(ratings));
		_refreshAdmin = refreshAdmin ?? throw new ArgumentNullException(nameof(refreshAdmin));
		_applicationTimeOptions = applicationTimeOptions?.Value ?? throw new ArgumentNullException(nameof(applicationTimeOptions));
	}

	/// <summary>
	/// Возвращает список пользователей, отсортированный по соответствующему рейтингу.
	/// </summary>
	/// <remarks>
	/// Если <c>regionName</c> и <c>settlementName</c> не заданы, используется <c>OverallRank</c>.
	/// Если задан только <c>regionName</c>, используется <c>RegionRank</c>.
	/// Если заданы оба параметра, используется <c>SettlementRank</c>.
	/// Передавать только <c>settlementName</c> без <c>regionName</c> нельзя.
	/// </remarks>
	/// <param name="regionName">Опциональный фильтр по региону.</param>
	/// <param name="settlementName">Опциональный фильтр по населённому пункту. Допустим только вместе с <c>regionName</c>.</param>
	/// <param name="start">Начальный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="end">Конечный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Список пользователей рейтинга успешно возвращён.</response>
	/// <response code="400">Переданы некорректные параметры фильтрации или пагинации.</response>
	[HttpGet("feed")]
	[ProducesResponseType(typeof(IReadOnlyList<UserRatingFeedItemDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetFeed(
		[FromQuery] string? regionName,
		[FromQuery] string? settlementName,
		[FromQuery] int? start = null,
		[FromQuery] int? end = null,
		CancellationToken cancellationToken = default)
	{
		var invalidModel = TryBuildValidationProblemIfInvalidModel();
		if(invalidModel is not null)
		{
			return invalidModel;
		}

		var invalidRequest = TryBuildRatingsFeedValidationProblem(regionName, settlementName, start, end);
		if(invalidRequest is not null)
		{
			return invalidRequest;
		}

		var result = await _ratings.GetFeedAsync(regionName, settlementName, start, end, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapUserRatingsError(result.Error);
		}

		var dtos = (result.Value ?? Array.Empty<UserRatingFeedItemModel>())
			.Select(ToDto)
			.ToList();

		return Ok(dtos);
	}

	/// <summary>
	/// Возвращает все три места пользователя в рейтингах.
	/// </summary>
	/// <param name="userId">Идентификатор пользователя.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Места пользователя успешно возвращены.</response>
	/// <response code="400">Передан некорректный идентификатор пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
	[HttpGet("{userId:guid}")]
	[ProducesResponseType(typeof(UserRatingSummaryResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetUserRanks(
		[FromRoute] Guid userId,
		CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["userId"] = new[] { "UserId must be non-empty GUID." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте корректный userId.");
		}

		var result = await _ratings.GetUserRanksAsync(userId, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapUserRatingsError(result.Error);
		}

		var summary = result.Value!;
		return Ok(new UserRatingSummaryResponse(
			summary.UserId,
			summary.OverallRank,
			summary.RegionRank,
			summary.SettlementRank));
	}

	/// <summary>
	/// Возвращает текущее ежедневное расписание пересчёта пользовательских рейтингов.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	[HttpGet("refresh/schedule")]
	[ProducesResponseType(typeof(UserRatingsRefreshScheduleResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> GetRefreshSchedule(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _refreshAdmin.GetRefreshScheduleAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapUserRatingsAdminError(result.Error);
		}

		return Ok(ToScheduleResponse(result.Value!));
	}

	/// <summary>
	/// Обновляет ежедневное расписание пересчёта пользовательских рейтингов.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	[HttpPut("refresh/schedule")]
	[ProducesResponseType(typeof(UserRatingsRefreshScheduleResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> UpdateRefreshSchedule(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] UpdateUserRatingsRefreshScheduleRequest? request,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		if(request is null)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["body"] = new[] { "Request body is required." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте body с полями hour и minute.");
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(request.Hour is < 0 or > 23)
		{
			errors["hour"] = new[] { "Hour must be in range 0..23." };
		}

		if(request.Minute is < 0 or > 59)
		{
			errors["minute"] = new[] { "Minute must be in range 0..59." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте диапазоны полей hour и minute.");
		}

		var result = await _refreshAdmin.UpdateRefreshScheduleAsync(
			actorUserId,
			actorUserPassword!,
			request.Hour,
			request.Minute,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapUserRatingsAdminError(result.Error);
		}

		return Ok(ToScheduleResponse(result.Value!));
	}

	/// <summary>
	/// Принудительно запускает немедленный пересчёт пользовательских рейтингов.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	[HttpPost("refresh/run")]
	[ProducesResponseType(typeof(RunUserRatingsRefreshResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> RunRefreshNow(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _refreshAdmin.RunRefreshNowAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapUserRatingsAdminError(result.Error);
		}

		return Ok(ToRunResponse(result.Value!));
	}

	private static UserRatingFeedItemDto ToDto(UserRatingFeedItemModel model)
		=> new(
			model.Id,
			model.LastName,
			model.FirstName,
			model.MiddleName,
			model.Gender,
			model.PhoneNumber,
			model.BirthDate,
			model.RegionName,
			model.SettlementName,
			model.Role,
			model.IsPhoneConfirmed,
			model.Rank)
		{
			AvatarImageUrl = model.AvatarImageUrl,
			Balance = model.Balance,
		};

	private UserRatingsRefreshScheduleResponse ToScheduleResponse(UserRatingsRefreshScheduleModel model)
	{
		var culture = ApplicationTimeCultureConfigurator.ResolveCulture(_applicationTimeOptions.Locale);
		var localTime = new TimeOnly(model.Hour, model.Minute).ToString("t", culture);

		return new UserRatingsRefreshScheduleResponse(
			model.Hour,
			model.Minute,
			localTime,
			culture.Name,
			model.LastCompletedLocalDate,
			model.LastCompletedAtUtc);
	}

	private static RunUserRatingsRefreshResponse ToRunResponse(UserRatingsRefreshRunModel model)
		=> new(
			model.StartedAtUtc,
			model.CompletedAtUtc,
			model.TotalUsers,
			model.CreatedMissingRows,
			model.UpdatedUsers);

	private IActionResult? TryBuildRatingsFeedValidationProblem(
		string? regionName,
		string? settlementName,
		int? start,
		int? end)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(regionName is not null && string.IsNullOrWhiteSpace(regionName))
		{
			errors["regionName"] = new[] { "RegionName must not be empty." };
		}

		if(settlementName is not null)
		{
			var settlementErrors = new List<string>();

			if(string.IsNullOrWhiteSpace(settlementName))
			{
				settlementErrors.Add("SettlementName must not be empty.");
			}

			if(string.IsNullOrWhiteSpace(regionName))
			{
				settlementErrors.Add("settlementName can be used only together with regionName.");
			}

			if(settlementErrors.Count > 0)
			{
				errors["settlementName"] = settlementErrors.ToArray();
			}
		}

		if(start is null ^ end is null)
		{
			if(start is null)
			{
				errors["start"] = new[] { "Start is required when end is specified." };
			}

			if(end is null)
			{
				errors["end"] = new[] { "End is required when start is specified." };
			}
		}
		else if(start is not null && end is not null)
		{
			if(start.Value <= 0)
			{
				errors["start"] = new[] { "Start must be positive." };
			}

			if(end.Value <= 0)
			{
				errors["end"] = new[] { "End must be positive." };
			}

			if(end.Value < start.Value)
			{
				errors["end"] = new[] { "End must be greater or equal to start." };
			}
		}

		return errors.Count == 0
			? null
			: this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте параметры regionName/settlementName/start/end. settlementName допускается только вместе с regionName, пагинация задаётся парой start/end.");
	}

	private IActionResult? TryBuildValidationProblemIfInvalidModel()
	{
		if(ModelState.IsValid)
		{
			return null;
		}

		var errors = ModelState
			.Where(x => x.Value?.Errors.Count > 0)
			.ToDictionary(
				x => x.Key,
				x => x.Value!.Errors
					.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
					.ToArray());

		return this.ValidationProblemWithCode(
			ApiErrorCodes.ValidationFailed,
			errors,
			title: "Некорректный запрос.",
			detail: "Проверьте query-параметры и их допустимые значения.");
	}

	private IActionResult MapUserRatingsAdminError(UserRatingsAdminError error)
		=> error switch
		{
			UserRatingsAdminError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				"Проверьте actorUserId, заголовок пароля и параметры расписания."),

			UserRatingsAdminError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),

			UserRatingsAdminError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Операция доступна только пользователю с ролью admin."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};

	private IActionResult MapUserRatingsError(UserRatingsError error)
		=> error switch
		{
			UserRatingsError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				"Проверьте параметры фильтрации, пагинации и userId."),

			UserRatingsError.UserNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserNotFound,
				"Пользователь не найден."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
}