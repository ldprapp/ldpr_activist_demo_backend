using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Tasks;
using LdprActivistDemo.Contracts.Users;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

[ApiController]
[Route("api/v1/tasks")]
public sealed class TasksController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly ITaskService _tasks;

	public TasksController(ITaskService tasks)
	{
		_tasks = tasks;
	}

	[HttpPost]
	[ProducesResponseType(typeof(CreateTaskResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] CreateTaskRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null)
		{
			return invalid;
		}

		var model = new TaskCreateModel(
			request.Title,
			request.Description,
			request.RequirementsText,
			request.RewardPoints,
			request.CoverImageUrl,
			request.ExecutionLocation,
			request.PublishedAt,
			request.DeadlineAt,
			request.Status,
			request.RegionId,
			request.CityId,
			request.TrustedAdminIds ?? Array.Empty<Guid>());

		var result = await _tasks.CreateAsync(actorUserId, actorUserPassword!, model, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapTaskError(result.Error);
		}

		return Created($"/api/v1/tasks/{result.Value}", new CreateTaskResponse(result.Value));
	}

	[HttpPut("{taskId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] UpdateTaskRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null)
		{
			return invalid;
		}

		var model = new TaskUpdateModel(
			request.Title,
			request.Description,
			request.RequirementsText,
			request.RewardPoints,
			request.CoverImageUrl,
			request.ExecutionLocation,
			request.PublishedAt,
			request.DeadlineAt,
			request.Status,
			request.RegionId,
			request.CityId,
			request.TrustedAdminIds ?? Array.Empty<Guid>());

		var result = await _tasks.UpdateAsync(actorUserId, actorUserPassword!, taskId, model, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	[HttpDelete("{taskId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);

		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _tasks.DeleteAsync(actorUserId, actorUserPassword!, taskId, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	[HttpPost("{taskId:guid}/close")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CloseAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _tasks.CloseAsync(actorUserId, actorUserPassword!, taskId, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	[HttpGet("{taskId:guid}")]
	[ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAsync(
		[FromRoute] Guid taskId,
		CancellationToken cancellationToken)
	{
		var result = await _tasks.GetPublicAsync(taskId, cancellationToken);
		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		return Ok(ToDto(result.Value));
	}

	[HttpGet("feed/by-region")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskCardDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetRegionFeedAsync([FromQuery] int regionId, CancellationToken cancellationToken)
	{
		if(regionId <= 0)
		{
			var errors = new Dictionary<string, string[]>
			{
				["regionId"] = new[] { "RegionId must be positive." },
			};

			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "regionId должен быть положительным числом.");
		}

		var tasks = await _tasks.GetByRegionAsync(regionId, cancellationToken);
		return Ok(tasks.Select(ToCardDto).ToList());
	}

	[HttpGet("feed/by-city")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskCardDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetCityFeedAsync([FromQuery] int regionId, [FromQuery] int cityId, CancellationToken cancellationToken)
	{
		if(regionId <= 0 || cityId <= 0)
		{
			var errors = new Dictionary<string, string[]>
			{
				["regionId"] = regionId <= 0 ? new[] { "RegionId must be positive." } : Array.Empty<string>(),
				["cityId"] = cityId <= 0 ? new[] { "CityId must be positive." } : Array.Empty<string>(),
			};

			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "regionId и cityId должны быть положительными числами.");
		}

		var tasks = await _tasks.GetByRegionAndCityAsync(regionId, cityId, cancellationToken);
		return Ok(tasks.Select(ToCardDto).ToList());
	}

	[HttpGet("available")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskCardDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetAvailableAsync(
		[FromQuery] Guid userId,
		CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["userId"] = new[] { "UserId is required." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте userId параметром запроса.");
		}

		var result = await _tasks.GetAvailableForUserAsync(userId, cancellationToken);
		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		return Ok(result.Value.Select(ToCardDto).ToList());
	}

	[HttpGet("by-user/submitted")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskCardDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> GetTasksByUserSubmittedAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var result = await _tasks.GetByUserSubmittedAsync(actorUserId, actorUserPassword!, cancellationToken);
		return result.IsSuccess && result.Value is not null
			? Ok(result.Value.Select(ToCardDto).ToList())
			: MapTaskError(result.Error);
	}

	[HttpGet("by-user/approved")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskCardDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> GetTasksByUserApprovedAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var result = await _tasks.GetByUserApprovedAsync(actorUserId, actorUserPassword!, cancellationToken);
		return result.IsSuccess && result.Value is not null
			? Ok(result.Value.Select(ToCardDto).ToList())
			: MapTaskError(result.Error);
	}

	[HttpGet("feed/by-admin")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskCardDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetAdminFeedAsync([FromQuery] Guid adminUserId, CancellationToken cancellationToken)
	{
		if(adminUserId == Guid.Empty)
		{
			var errors = new Dictionary<string, string[]>
			{
				["adminUserId"] = new[] { "AdminUserId is required." },
			};

			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "adminUserId обязателен.");
		}

		var tasks = await _tasks.GetByAdminAsync(adminUserId, cancellationToken);
		return Ok(tasks.Select(ToCardDto).ToList());
	}

	[HttpPost("{taskId:guid}/submit")]
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> SubmitAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] SubmitTaskRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null)
		{
			return invalid;
		}

		var model = new TaskSubmissionCreateModel(
			PhotoUrls: request.PhotoUrls,
			ProofText: request.ProofText,
			SubmittedAt: request.SubmittedAt == default
				? DateTimeOffset.UtcNow
				: request.SubmittedAt);

		var result = await _tasks.SubmitAsync(actorUserId, actorUserPassword!, taskId, model, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapTaskError(result.Error);
		}

		return StatusCode(StatusCodes.Status201Created);
	}

	[HttpPut("{taskId:guid}/submission")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UpdateSubmissionAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] SubmitTaskRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null) return invalid;

		var model = new TaskSubmissionCreateModel(
			PhotoUrls: request.PhotoUrls,
			ProofText: request.ProofText,
			SubmittedAt: request.SubmittedAt == default ? DateTimeOffset.UtcNow : request.SubmittedAt);

		var result = await _tasks.UpdateSubmissionAsync(actorUserId, actorUserPassword!, taskId, model, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	[HttpGet("{taskId:guid}/submitted")]
	[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetSubmittedUsersAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var result = await _tasks.GetSubmittedUsersAsync(actorUserId, actorUserPassword!, taskId, cancellationToken);
		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		return Ok(result.Value.Select(ToPublicDto).ToList());
	}

	[HttpGet("{taskId:guid}/approved")]
	[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetApprovedUsersAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var result = await _tasks.GetApprovedUsersAsync(actorUserId, actorUserPassword!, taskId, cancellationToken);
		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		return Ok(result.Value.Select(ToPublicDto).ToList());
	}

	[HttpGet("{taskId:guid}/submitted/{userId:guid}")]
	[ProducesResponseType(typeof(SubmissionUserViewDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetSubmittedUserAsync(
		[FromRoute] Guid taskId,
		[FromRoute] Guid userId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var result = await _tasks.GetSubmittedUserAsync(actorUserId, actorUserPassword!, taskId, userId, cancellationToken);
		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		return Ok(new SubmissionUserViewDto(ToPublicDto(result.Value.User), ToDto(result.Value.Submission)));
	}

	[HttpPost("{taskId:guid}/approve")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ApproveAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromQuery] Guid userId,
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
					["userId"] = new[] { "UserId is required." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте userId параметром запроса.");
		}

		var result = await _tasks.ApproveAsync(actorUserId, actorUserPassword!, taskId, userId, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	private IActionResult? TryBuildActorValidationProblem(Guid actorUserId, string? actorUserPassword)
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

		return errors.Count == 0
			? null
			: this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: $"Передайте actorUserId и заголовок {ActorPasswordHeader}.");
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
				x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());

		return this.ValidationProblemWithCode(
			ApiErrorCodes.ValidationFailed,
			errors,
			title: "Некорректный запрос.",
			detail: "Проверьте ограничения валидации (Required/Range) и значения полей.");
	}

	private IActionResult MapTaskError(TaskOperationError error)
		=> error switch
		{
			TaskOperationError.ValidationFailed => this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed, "Некорректный запрос.", "Проверьте тело запроса и параметры."),
			TaskOperationError.InvalidCredentials => this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверные учётные данные.", $"Проверьте id пользователя и заголовок {ActorPasswordHeader}."),
			TaskOperationError.Forbidden => this.ProblemWithCode(StatusCodes.Status403Forbidden, ApiErrorCodes.Forbidden, "Нет доступа.", "Операция запрещена: требуется автор задачи или назначенный ответственный администратор."),
			TaskOperationError.TaskNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.TaskNotFound, "Задача не найдена."),
			TaskOperationError.RegionNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.GeoRegionNotFound, "Регион не найден."),
			TaskOperationError.CityRegionMismatch => this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.CityRegionMismatch, "Город не принадлежит региону.", "Указанный город должен относиться к указанному региону."),
			TaskOperationError.TaskClosed => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskClosed, "Задача закрыта.", "Операция недоступна для закрытой задачи."),
			TaskOperationError.AlreadySubmitted => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskAlreadySubmitted, "Заявка уже подтверждена.", "Нельзя редактировать подтверждённую заявку."),
			TaskOperationError.SubmissionAlreadyExists => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskSubmissionExists, "Заявка уже существует.", "Повторная отправка запрещена. Используйте PUT /api/v1/tasks/{taskId}/submission для редактирования (пока заявка не подтверждена)."),
			TaskOperationError.SubmissionNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.TaskSubmissionNotFound, "Заявка не найдена."),
			TaskOperationError.UserNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.", "Пользователь не существует или один из TrustedAdminIds не является администратором."),
			_ => this.ProblemWithCode(StatusCodes.Status500InternalServerError, ApiErrorCodes.InternalError, "Внутренняя ошибка."),
		};

	private static TaskDto ToDto(TaskModel t)
		=> new(
			t.Id,
			t.AuthorUserId,
			t.Title,
			t.Description,
			t.RequirementsText,
			t.RewardPoints,
			t.CoverImageUrl,
			t.ExecutionLocation,
			t.PublishedAt,
			t.DeadlineAt,
			t.Status,
			t.RegionId,
			t.CityId,
			t.TrustedAdminIds);

	private static TaskCardDto ToCardDto(TaskModel t)
		=> new(
			t.Id,
			t.Title,
			t.Description,
			t.RewardPoints,
			t.CoverImageUrl,
			t.DeadlineAt,
			t.Status,
			t.RegionId,
			t.CityId);

	private static UserDto ToPublicDto(UserPublicModel u)
		=> new(
			u.Id,
			u.LastName,
			u.FirstName,
			u.MiddleName,
			u.Gender,
			u.PhoneNumber,
			u.BirthDate,
			u.RegionId,
			u.CityId,
			u.IsPhoneConfirmed,
			u.Points);

	private static SubmissionDto ToDto(TaskSubmissionModel s)
		=> new(
			s.Id,
			s.TaskId,
			s.UserId,
			s.SubmittedAt,
			s.ConfirmedByAdminId,
			s.ConfirmedAt,
			s.PhotoUrls,
			s.ProofText);
}