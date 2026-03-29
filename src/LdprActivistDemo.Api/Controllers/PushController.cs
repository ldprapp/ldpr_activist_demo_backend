using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Application.Push;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Push;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

[ApiController]
[Route("api/v1/push")]
public sealed class PushController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IPushDeviceService _pushDevices;

	public PushController(IPushDeviceService pushDevices)
	{
		_pushDevices = pushDevices ?? throw new ArgumentNullException(nameof(pushDevices));
	}

	[HttpPut("device")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> RegisterDeviceAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] RegisterPushDeviceRequest? request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
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
				detail: "Передайте тело запроса с полями token и platform.");
		}

		var invalidModel = TryBuildValidationProblemIfInvalidModel();
		if(invalidModel is not null)
		{
			return invalidModel;
		}

		var result = await _pushDevices.RegisterAsync(
			actorUserId,
			actorUserPassword!,
			request.Token,
			request.Platform,
			cancellationToken);

		return result.IsSuccess
			? NoContent()
			: MapPushDeviceError(result.Error);
	}

	[HttpPost("device/deactivate")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> DeactivateDeviceAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] DeactivatePushDeviceRequest? request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
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
				detail: "Передайте тело запроса с полем token.");
		}

		var invalidModel = TryBuildValidationProblemIfInvalidModel();
		if(invalidModel is not null)
		{
			return invalidModel;
		}

		var result = await _pushDevices.DeactivateAsync(
			actorUserId,
			actorUserPassword!,
			request.Token,
			cancellationToken);

		return result.IsSuccess
			? NoContent()
			: MapPushDeviceError(result.Error);
	}

	private IActionResult? TryBuildActorValidationProblem(
		Guid actorUserId,
		string? actorUserPassword)
		=> this.TryBuildActorRequestValidationProblem(
			actorUserId,
			actorUserPassword,
			ActorPasswordHeader);

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
					.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
						? "Invalid value."
						: e.ErrorMessage)
					.ToArray());

		return this.ValidationProblemWithCode(
			ApiErrorCodes.ValidationFailed,
			errors,
			title: "Некорректный запрос.",
			detail: "Проверьте тело запроса и обязательные поля.");
	}

	private IActionResult MapPushDeviceError(PushDeviceOperationError error)
		=> error switch
		{
			PushDeviceOperationError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				$"Проверьте actorUserId, заголовок {ActorPasswordHeader} и тело запроса."),

			PushDeviceOperationError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),

			PushDeviceOperationError.TokenInvalid => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.PushTokenInvalid,
				"Некорректный push token.",
				"Поле token обязательно и должно содержать непустую строку."),

			PushDeviceOperationError.PlatformInvalid => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.PushPlatformInvalid,
				"Некорректная push-платформа.",
				$"Поле platform допускает только '{PushPlatform.Android}', '{PushPlatform.Ios}' или '{PushPlatform.Web}'."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
}