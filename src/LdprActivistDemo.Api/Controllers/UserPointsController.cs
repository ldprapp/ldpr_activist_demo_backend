using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Application.UserPoints;
using LdprActivistDemo.Application.UserPoints.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.UserPoints;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

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

	[HttpGet("balance")]
	[ProducesResponseType(typeof(UserPointsBalanceResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetBalanceAsync(
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

		var result = await _points.GetBalanceAsync(actorUserId, actorUserPassword!, userId, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapPointsError(result.Error);
		}

		return Ok(new UserPointsBalanceResponse(result.Value));
	}

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

	[HttpPost("transactions")]
	[ProducesResponseType(typeof(CreateUserPointsTransactionResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateTransactionAsync(
		[FromRoute] Guid userId,
		[FromQuery] Guid actorUserId,
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
				detail: "Проверьте поля amount и comment.");
		}

		var result = await _points.CreateTransactionAsync(
			actorUserId,
			actorUserPassword!,
			userId,
			request.Amount,
			comment,
			cancellationToken);

		if(!result.IsSuccess)
		{
			return MapPointsError(result.Error);
		}

		var id = result.Value;
		return Created($"/api/v1/users/{userId}/points/transactions/{id}", new CreateUserPointsTransactionResponse(id));
	}

	private static UserPointsTransactionDto ToDto(UserPointsTransactionModel t)
		=> new(t.Id, t.Amount, t.TransactionAt, t.Comment);

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
				"Операция доступна только администратору."),

			UserPointsError.InsufficientBalance => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.UserPointsInsufficientBalance,
				"Недостаточно баллов.",
				"Операция приводит к отрицательному балансу."),

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