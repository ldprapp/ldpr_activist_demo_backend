using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.RateLimiting;
using LdprActivistDemo.Application.Referrals;
using LdprActivistDemo.Application.Referrals.Models;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Referrals;
using LdprActivistDemo.Contracts.Users;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Эндпоинты реферальной системы.
/// </summary>
[ApiController]
[Route("api/v1/referrals")]
public sealed class ReferralsController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IReferralService _referrals;

	public ReferralsController(IReferralService referrals)
	{
		_referrals = referrals ?? throw new ArgumentNullException(nameof(referrals));
	}

	/// <summary>
	/// Возвращает реферальный контент пользователя: либо сам код, либо готовый текст приглашения.
	/// </summary>
	/// <remarks>
	/// Пользователь с ролями <c>activist</c> и <c>coordinator</c> может получить только собственный
	/// реферальный контент. Пользователь с ролью <c>admin</c> может получить реферальный контент
	/// любого пользователя. Параметр <c>responseFormat</c> принимает значения <c>code</c> или <c>text</c>.
	/// При формате <c>text</c> маркер <c>{code}</c> заменяется на реферальный код пользователя,
	/// а маркер <c>{reward}</c> — на текущее значение бонуса для приглашённого пользователя.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="userId">Идентификатор пользователя, для которого возвращается реферальный контент.</param>
	/// <param name="responseFormat">Формат ответа: <c>code</c> или <c>text</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Реферальный контент успешно возвращён.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Нет прав на получение реферального контента указанного пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
	[HttpGet("content")]
	[ProducesResponseType(typeof(ReferralContentResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<ReferralContentResponse>> GetContent(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] Guid userId,
		[FromQuery] string? responseFormat,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		if(userId == Guid.Empty)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["userId"] = new[] { "UserId is required." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте userId.");
		}

		if(!TryNormalizeResponseFormat(responseFormat, out var normalizedResponseFormat, out var responseFormatError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["responseFormat"] = new[] { responseFormatError! },
				},
				title: "Некорректный запрос.",
				detail: "responseFormat допускает только значения 'code' или 'text'.");
		}

		var result = await _referrals.GetContentAsync(
			actorUserId,
			actorUserPassword!,
			userId,
			normalizedResponseFormat,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapReferralContentError(result.Error);
		}

		return Ok(new ReferralContentResponse(result.Content));
	}

	/// <summary>
	/// Возвращает текущие административные настройки реферальной системы.
	/// </summary>
	/// <remarks>
	/// Эндпоинт доступен любому пользователю без аутентификации.
	/// В ответ входят текущий шаблон текста приглашения, награда для приглашающего
	/// и награда для приглашённого пользователя.
	/// </remarks>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Текущие настройки успешно возвращены.</response>
	[HttpGet("settings")]
	[ProducesResponseType(typeof(ReferralSettingsResponse), StatusCodes.Status200OK)]
	public async Task<ActionResult<ReferralSettingsResponse>> GetSettings(
		CancellationToken cancellationToken)
	{
		var result = await _referrals.GetSettingsAsync(cancellationToken);

		return Ok(new ReferralSettingsResponse(
			result.InviteTextTemplate,
			result.InviterRewardPoints,
			result.InvitedUserRewardPoints));
	}

	/// <summary>
	/// Возвращает список пользователей, приглашённых указанным пользователем.
	/// </summary>
	/// <remarks>
	/// Пользователь с ролями <c>activist</c> и <c>coordinator</c> может получить только собственный
	/// список приглашённых пользователей. Пользователь с ролью <c>admin</c> может получить такой
	/// список для любого пользователя.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="userId">Идентификатор пользователя, для которого возвращается список приглашённых.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Список приглашённых пользователей успешно возвращён.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Нет прав на получение списка приглашённых указанного пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
	[HttpGet("invited-users")]
	[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<IReadOnlyList<UserDto>>> GetInvitedUsers(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] Guid userId,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(
			actorUserId,
			actorUserPassword,
			ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		if(userId == Guid.Empty)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["userId"] = new[] { "UserId is required." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте userId.");
		}

		var result = await _referrals.GetInvitedUsersAsync(
			actorUserId,
			actorUserPassword!,
			userId,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapReferralInvitedUsersReadError(result.Error);
		}

		return Ok(result.Users.Select(ToUserDto).ToList());
	}

	/// <summary>
	/// Атомарно обновляет все административные настройки реферальной системы.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// В шаблоне обязательно должен присутствовать маркер <c>{code}</c>, который будет заменён
	/// на реферальный код пользователя. Дополнительно поддерживается маркер <c>{reward}</c>,
	/// который будет заменён на текущее значение бонуса для приглашённого пользователя.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор администратора, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль администратора из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">
	/// Тело запроса с новым шаблоном текста приглашения, количеством баллов для приглашающего
	/// и количеством баллов для приглашённого пользователя.
	/// </param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Настройки успешно обновлены.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Операция доступна только администратору.</response>
	[HttpPut("settings")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(typeof(ReferralSettingsResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<ReferralSettingsResponse>> UpdateSettings(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] UpdateReferralSettingsRequest? request,
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
				});
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(string.IsNullOrWhiteSpace(request.InviteTextTemplate))
		{
			errors[nameof(request.InviteTextTemplate)] = new[] { "InviteTextTemplate is required." };
		}
		else if(!request.InviteTextTemplate.Contains(ReferralDefaults.CodePlaceholder, StringComparison.Ordinal))
		{
			errors[nameof(request.InviteTextTemplate)] = new[]
			{
				$"InviteTextTemplate must contain '{ReferralDefaults.CodePlaceholder}' placeholder.",
			};
		}

		if(request.InviterRewardPoints < 0)
		{
			errors[nameof(request.InviterRewardPoints)] = new[]
			{
				"InviterRewardPoints must be greater or equal to zero.",
			};
		}

		if(request.InvitedUserRewardPoints < 0)
		{
			errors[nameof(request.InvitedUserRewardPoints)] = new[]
			{
				"InvitedUserRewardPoints must be greater or equal to zero.",
			};
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail:
					$"Шаблон должен быть непустым и содержать маркер {ReferralDefaults.CodePlaceholder}. " +
					$"Маркер {ReferralDefaults.RewardPlaceholder} поддерживается дополнительно для подстановки текущей награды приглашённому пользователю. " +
					$"Значения наград не могут быть отрицательными.");
		}

		var result = await _referrals.UpdateSettingsAsync(
			actorUserId,
			actorUserPassword!,
			request.InviteTextTemplate,
			request.InviterRewardPoints,
			request.InvitedUserRewardPoints,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapReferralSettingsUpdateError(result.Error);
		}

		return Ok(new ReferralSettingsResponse(
			result.InviteTextTemplate,
			result.InviterRewardPoints,
			result.InvitedUserRewardPoints));
	}

	private static bool TryNormalizeResponseFormat(
		string? raw,
		out ReferralContentFormat format,
		out string? error)
	{
		error = null;
		format = ReferralContentFormat.Code;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return true;
		}

		var token = raw.Trim().ToLowerInvariant();
		switch(token)
		{
			case "code":
				format = ReferralContentFormat.Code;
				return true;
			case "text":
				format = ReferralContentFormat.Text;
				return true;
			default:
				error = "ResponseFormat must be 'code' or 'text'.";
				return false;
		}
	}

	private ActionResult MapReferralContentError(ReferralContentError error)
		=> error switch
		{
			ReferralContentError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				$"Проверьте actorUserId, userId и заголовок {ActorPasswordHeader}."),

			ReferralContentError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),

			ReferralContentError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Пользователь с ролями activist и coordinator может получить только собственный реферальный контент. Пользователь с ролью admin может получить контент любого пользователя."),

			ReferralContentError.UserNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserNotFound,
				"Пользователь не найден."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};

	private ActionResult MapReferralSettingsUpdateError(ReferralSettingsUpdateError error)
		=> error switch
		{
			ReferralSettingsUpdateError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				$"Проверьте actorUserId, заголовок {ActorPasswordHeader}, шаблон текста приглашения и значения наград."),

			ReferralSettingsUpdateError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),

			ReferralSettingsUpdateError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Операция доступна только пользователю с ролью admin."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};

	private ActionResult MapReferralInvitedUsersReadError(ReferralInvitedUsersReadError error)
		=> error switch
		{
			ReferralInvitedUsersReadError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				$"Проверьте actorUserId, userId и заголовок {ActorPasswordHeader}."),

			ReferralInvitedUsersReadError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),

			ReferralInvitedUsersReadError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Пользователь с ролями activist и coordinator может получить только собственный список приглашённых пользователей. Пользователь с ролью admin может получить список приглашённых для любого пользователя."),

			ReferralInvitedUsersReadError.UserNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserNotFound,
				"Пользователь не найден."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};

	private static UserDto ToUserDto(UserPublicModel u)
		=> new(
			u.Id,
			u.LastName,
			u.FirstName,
			u.MiddleName,
			u.Gender,
			u.PhoneNumber,
			u.BirthDate,
			u.RegionName,
			u.SettlementName,
			u.Role,
			u.IsPhoneConfirmed)
		{
			AvatarImageUrl = u.AvatarImageUrl,
		};
}