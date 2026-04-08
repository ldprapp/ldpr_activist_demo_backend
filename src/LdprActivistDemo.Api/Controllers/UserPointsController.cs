using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.RateLimiting;
using LdprActivistDemo.Application.UserPoints;
using LdprActivistDemo.Application.UserPoints.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.UserPoints;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Эндпоинты баланса и истории транзакций баллов пользователя.
/// </summary>
[ApiController]
[Route("api/v1/users/{userId:guid}/points")]
public sealed class UserPointsController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IUserPointsService _points;

	public UserPointsController(IUserPointsService points)
	{
		_points = points ?? throw new ArgumentNullException(nameof(points));
	}

	/// <summary>
	/// Возвращает текущий баланс баллов пользователя.
	/// </summary>
	/// <remarks>
	/// Баланс пользователя доступен любому пользователю без аутентификации.
	/// </remarks>
	/// <param name="userId">Идентификатор пользователя, чей баланс требуется получить.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Баланс успешно возвращён.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="404">Пользователь не найден.</response>
	[HttpGet("balance")]
	[ProducesResponseType(typeof(UserPointsBalanceResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetBalanceAsync(
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

		var result = await _points.GetBalanceAsync(userId, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapPointsError(result.Error);
		}

		return Ok(new UserPointsBalanceResponse(result.Value));
	}

	/// <summary>
	/// Возвращает историю транзакций баллов пользователя.
	/// </summary>
	/// <remarks>
	/// История транзакций доступна только самому пользователю или пользователю с ролью администратора.
	/// </remarks>
	/// <param name="userId">Идентификатор пользователя, чьи транзакции требуется получить.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего запрос.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">История транзакций успешно возвращена.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет права просматривать транзакции указанного пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
	[HttpGet("transactions")]
	[ProducesResponseType(typeof(IReadOnlyList<UserPointsTransactionDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetTransactionsAsync(
		[FromRoute] Guid userId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
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
					["userId"] = new[] { "UserId must be non-empty GUID." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте корректный userId.");
		}

		var result = await _points.GetTransactionsAsync(actorUserId, actorUserPassword!, userId, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapPointsError(result.Error);
		}

		var dtos = (result.Value ?? Array.Empty<UserPointsTransactionModel>())
			.Select(ToDto)
			.ToList();

		return Ok(dtos);
	}

	/// <summary>
	/// Создаёт ручную транзакцию начисления или списания баллов пользователю.
	/// </summary>
	/// <remarks>
	/// Операция предназначена для координатора или администратора. Параметр <c>coordinatorUserId</c>
	/// должен совпадать с <c>actorUserId</c>. Сумма не может быть равна нулю.
	/// </remarks>
	/// <param name="userId">Идентификатор пользователя, для которого создаётся транзакция.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="coordinatorUserId">Идентификатор координатора, который оформляет транзакцию. Должен совпадать с <c>actorUserId</c>.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Тело запроса с суммой и комментарием транзакции.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="201">Транзакция успешно создана.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет права создавать транзакции баллов.</response>
	/// <response code="404">Пользователь не найден.</response>
	[HttpPost("transactions")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(typeof(CreateUserPointsTransactionResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateTransactionAsync(
		[FromRoute] Guid userId,
		[FromQuery] Guid actorUserId,
		[FromQuery] Guid coordinatorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] CreateUserPointsTransactionRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
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
					["userId"] = new[] { "UserId must be non-empty GUID." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте корректный userId.");
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

		var comment = (request.Comment ?? string.Empty).Trim();
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(coordinatorUserId == Guid.Empty)
		{
			errors["coordinatorUserId"] = new[] { "CoordinatorUserId must be non-empty GUID." };
		}
		else if(coordinatorUserId != actorUserId)
		{
			errors["coordinatorUserId"] = new[] { "CoordinatorUserId must be equal to actorUserId." };
		}

		if(request.Amount == 0)
		{
			errors["amount"] = new[] { "Amount must be non-zero." };
		}

		if(comment.Length == 0)
		{
			errors["comment"] = new[] { "Comment is required." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте поля coordinatorUserId, amount и comment.");
		}

		var result = await _points.CreateTransactionAsync(
			actorUserId,
			actorUserPassword!,
			userId,
			request.Amount,
			comment,
			coordinatorUserId,
			taskId: null,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapPointsError(result.Error);
		}

		var id = result.Value;
		return Created($"/api/v1/users/{userId}/points/transactions/{id}", new CreateUserPointsTransactionResponse(id));
	}

	/// <summary>
	/// Отменяет транзакцию баллов пользователя.
	/// </summary>
	/// <remarks>
	/// Операция доступна только администратору.
	/// </remarks>
	[HttpPost("~/api/v1/user-points/transactions/{transactionId:guid}/cancel")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CancelTransactionAsync(
		[FromRoute] Guid transactionId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] CancelUserPointsTransactionRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(transactionId == Guid.Empty)
		{
			errors["transactionId"] = new[] { "TransactionId must be non-empty GUID." };
		}

		var cancellationComment = (request?.Comment ?? string.Empty).Trim();
		if(cancellationComment.Length == 0)
		{
			errors["comment"] = new[] { "Comment is required." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте поля transactionId и comment.");
		}

		var result = await _points.CancelTransactionAsync(
			actorUserId,
			actorUserPassword!,
			transactionId,
			cancellationComment,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapPointsError(result.Error);
		}

		return NoContent();
	}

	/// <summary>
	/// Возвращает ранее отменённую транзакцию баллов пользователя.
	/// </summary>
	/// <remarks>
	/// Операция доступна только администратору.
	/// </remarks>
	[HttpPost("~/api/v1/user-points/transactions/{transactionId:guid}/restore")]
	[EnableRateLimiting(ApiRateLimitingPolicyNames.AuthenticatedMutation)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> RestoreTransactionAsync(
		[FromRoute] Guid transactionId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(transactionId == Guid.Empty)
		{
			errors["transactionId"] = new[] { "TransactionId must be non-empty GUID." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте поле transactionId.");
		}

		var result = await _points.RestoreTransactionAsync(
			actorUserId,
			actorUserPassword!,
			transactionId,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapPointsError(result.Error);
		}

		return NoContent();
	}

	private static UserPointsTransactionDto ToDto(UserPointsTransactionModel t)
		=> new(
			t.Id,
			t.Amount,
			t.TransactionAt,
			t.Comment,
			t.CoordinatorUserId,
			t.TaskId,
			t.IsCancelled,
			t.CancellationComment,
			t.CancelledAtUtc,
			t.CancelledByAdminUserId);

	private IActionResult? TryBuildActorValidationProblem(Guid actorUserId, string? actorUserPassword)
		=> this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);

	private IActionResult MapPointsError(UserPointsError error)
		=> error switch
		{
			UserPointsError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				"Проверьте параметры и тело запроса."),

			UserPointsError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),

			UserPointsError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Операция не доступна данному пользователю."),

			UserPointsError.InsufficientBalance => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.UserPointsInsufficientBalance,
				"Недостаточно баллов.",
				"Операция приводит к отрицательному балансу."),

			UserPointsError.TransactionNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserPointsTransactionNotFound,
				"Транзакция не найдена.",
				"Транзакция не найдена для указанного пользователя."),

			UserPointsError.UserNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserNotFound,
				"Пользователь не найден."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
}