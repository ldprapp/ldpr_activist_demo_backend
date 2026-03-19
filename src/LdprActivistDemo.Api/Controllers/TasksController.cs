using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.Helpers;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Tasks;
using LdprActivistDemo.Contracts.Users;

using Microsoft.AspNetCore.Mvc;

using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Api.Controllers;

/// <summary>
/// Эндпоинты работы с задачами и заявками на выполнение задач.
/// </summary>
[ApiController]
[Route("api/v1/tasks")]
public sealed class TasksController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly ITaskService _tasks;
	private readonly ITaskFeedRepository _taskFeed;
	private readonly IImageService _images;

	public TasksController(ITaskService tasks, ITaskFeedRepository taskFeed, IImageService images)
	{
		_tasks = tasks;
		_taskFeed = taskFeed;
		_images = images;
	}

	public enum TaskFeedSort
	{
		None = 0,
		PublishedNewest = 1,
		PublishedOldest = 2,
		DeadlineSoonest = 3,
		DeadlineLatest = 4,
	}

	public enum TaskUsersResponseFormat
	{
		Users = 0,
		Count = 1,
	}

	public enum SubmissionFeedSort
	{
		None = 0,
		SubmittedNewest = 1,
		SubmittedOldest = 2,
		DecidedNewest = 3,
		DecidedOldest = 4,
	}

	private static DateTimeOffset? GetDeadline(TaskModel t) => t.DeadlineAt;

	private static bool IsDeadlineExpired(TaskModel t, DateTimeOffset nowUtc)
	{
		var d = GetDeadline(t);
		return d.HasValue && d.Value < nowUtc;
	}

	private static IReadOnlyList<TaskModel> ApplyDeadlineVisibilityAndSorting(
		IEnumerable<TaskModel> tasks,
		TaskFeedSort sort,
		bool includeExpiredDeadlines,
		DateTimeOffset nowUtc)
	{
		var list = tasks.ToList();

		if(!includeExpiredDeadlines)
		{
			list = list.Where(t => !IsDeadlineExpired(t, nowUtc)).ToList();
		}

		switch(sort)
		{
			case TaskFeedSort.PublishedNewest:
				return list
					.OrderByDescending(t => t.PublishedAt)
					.ThenBy(t => t.Id)
					.ToList();

			case TaskFeedSort.PublishedOldest:
				return list
					.OrderBy(t => t.PublishedAt)
					.ThenBy(t => t.Id)
					.ToList();

			case TaskFeedSort.DeadlineSoonest:
				{
					var upcoming = list
						.Where(t => !IsDeadlineExpired(t, nowUtc))
						.OrderBy(t => GetDeadline(t) ?? DateTimeOffset.MaxValue)
						.ThenBy(t => t.Id)
						.ToList();

					if(!includeExpiredDeadlines)
					{
						return upcoming;
					}

					var expired = list
						.Where(t => IsDeadlineExpired(t, nowUtc))
						.OrderBy(t => GetDeadline(t) ?? DateTimeOffset.MaxValue)
						.ThenBy(t => t.Id)
						.ToList();

					upcoming.AddRange(expired);
					return upcoming;
				}

			case TaskFeedSort.DeadlineLatest:
				{
					var upcoming = list
						.Where(t => !IsDeadlineExpired(t, nowUtc))
						.OrderByDescending(t => GetDeadline(t) ?? DateTimeOffset.MinValue)
						.ThenBy(t => t.Id)
						.ToList();

					if(!includeExpiredDeadlines)
					{
						return upcoming;
					}

					var expired = list
						.Where(t => IsDeadlineExpired(t, nowUtc))
						.OrderByDescending(t => GetDeadline(t) ?? DateTimeOffset.MinValue)
						.ThenBy(t => t.Id)
						.ToList();

					upcoming.AddRange(expired);
					return upcoming;
				}

			case TaskFeedSort.None:
			default:
				return list;
		}
	}

	/// <summary>
	/// Создаёт новую задачу.
	/// </summary>
	/// <remarks>
	/// Операция доступна пользователю с ролью <c>coordinator</c> или <c>admin</c>.
	/// Тело запроса передаётся как <c>multipart/form-data</c>, потому что задача может содержать обложку.
	/// Для <c>verificationType=auto</c> параметр <c>autoVerificationActionType</c> обязателен.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, создающего задачу.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Поля создаваемой задачи.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="201">Задача успешно создана.</response>
	/// <response code="400">Переданы некорректные параметры задачи, файлов или географии.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет прав на создание задач.</response>
	/// <response code="404">Один из связанных объектов не найден.</response>
	[HttpPost]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(CreateTaskResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromForm] CreateTaskFormRequest request,
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

		if(request.RewardPoints < 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["rewardPoints"] = new[] { "RewardPoints must be non-negative." },
				},
				title: "Некорректный запрос.",
				detail: "Награда (rewardPoints) не может быть отрицательной.");
		}

		if(!TryNormalizeTaskVerificationTypeForCreate(request.VerificationType, out var verificationType, out var verificationTypeError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["verificationType"] = new[] { verificationTypeError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр verificationType допускает только значения 'auto' или 'manual'.");
		}

		if(!TryNormalizeTaskReuseTypeForCreate(request.ReuseType, out var reuseType, out var reuseTypeError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["reuseType"] = new[] { reuseTypeError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр reuseType допускает только значения 'disposable' или 'reusable'.");
		}

		if(!TryNormalizeTaskAutoVerificationActionTypeForCreate(
			request.AutoVerificationActionType,
			verificationType,
			out var autoVerificationActionType,
			out var autoVerificationActionTypeError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["autoVerificationActionType"] = new[] { autoVerificationActionTypeError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр autoVerificationActionType обязателен для verificationType='auto' и допускает только 'invite_friend', 'first_login' или 'auto'.");
		}

		var auth = await _tasks.ValidateActorAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!auth.IsSuccess)
		{
			return MapTaskError(auth.Error);
		}

		Guid? coverImageId = null;
		if(request.CoverImage is not null)
		{
			var err = UploadedImageReader.ValidateImage(request.CoverImage);
			if(err is not null)
			{
				return this.ValidationProblemWithCode(
					ApiErrorCodes.ValidationFailed,
					new Dictionary<string, string[]>
					{
						["coverImage"] = new[] { err },
					});
			}

			var img = await UploadedImageReader.ReadAsync(request.CoverImage, cancellationToken);
			coverImageId = await _images.CreateAsync(actorUserId, img, cancellationToken);
		}

		var publishedAt = DateTimeOffset.UtcNow;

		var model = new TaskCreateModel(
			request.Title,
			request.Description,
			request.RequirementsText,
			request.RewardPoints,
			coverImageId,
			request.ExecutionLocation,
			publishedAt,
			request.DeadlineAt,
			request.RegionName,
			request.SettlementName,
			request.TrustedCoordinatorIds?.ToArray() ?? Array.Empty<Guid>(),
			verificationType,
			reuseType,
			autoVerificationActionType);

		var result = await _tasks.CreateAsync(actorUserId, actorUserPassword!, model, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapTaskError(result.Error);
		}

		return Created($"/api/v1/tasks/{result.Value}", new CreateTaskResponse(result.Value));
	}

	/// <summary>
	/// Обновляет существующую задачу.
	/// </summary>
	/// <remarks>
	/// Обновление доступно автору задачи, доверенному координатору задачи или администратору
	/// в соответствии с серверными правилами доступа. Тело запроса передаётся как <c>multipart/form-data</c>.
	/// </remarks>
	/// <param name="taskId">Идентификатор задачи.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Новые данные задачи.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Задача успешно обновлена.</response>
	/// <response code="400">Переданы некорректные параметры задачи, файлов или географии.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет прав на изменение задачи.</response>
	/// <response code="404">Задача или связанный объект не найдены.</response>
	[HttpPut("{taskId:guid}")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromForm] UpdateTaskFormRequest request,
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

		if(request.RewardPoints < 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["rewardPoints"] = new[] { "RewardPoints must be non-negative." },
				},
				title: "Некорректный запрос.",
				detail: "Награда (rewardPoints) не может быть отрицательной.");
		}

		if(!TryNormalizeTaskVerificationTypeForUpdate(request.VerificationType, out var verificationType, out var verificationTypeError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["verificationType"] = new[] { verificationTypeError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр verificationType допускает только значения 'auto' или 'manual'.");
		}

		if(!TryNormalizeTaskReuseTypeForUpdate(request.ReuseType, out var reuseType, out var reuseTypeError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["reuseType"] = new[] { reuseTypeError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр reuseType допускает только значения 'disposable' или 'reusable'.");
		}

		if(!TryNormalizeTaskAutoVerificationActionTypeForUpdate(
			request.AutoVerificationActionType,
			verificationType,
			out var autoVerificationActionType,
			out var autoVerificationActionTypeError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["autoVerificationActionType"] = new[] { autoVerificationActionTypeError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр autoVerificationActionType допускает только поддерживаемые значения ('invite_friend', 'first_login', 'auto'). Для verificationType='manual' игнорируется и сохраняется NULL.");
		}

		var auth = await _tasks.ValidateActorAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!auth.IsSuccess)
		{
			return MapTaskError(auth.Error);
		}

		Guid? coverImageId = null;
		if(request.CoverImage is not null)
		{
			var err = UploadedImageReader.ValidateImage(request.CoverImage);
			if(err is not null)
			{
				return this.ValidationProblemWithCode(
					ApiErrorCodes.ValidationFailed,
					new Dictionary<string, string[]>
					{
						["coverImage"] = new[] { err },
					});
			}

			var img = await UploadedImageReader.ReadAsync(request.CoverImage, cancellationToken);
			coverImageId = await _images.CreateAsync(actorUserId, img, cancellationToken);
		}

		var publishedAt = DateTimeOffset.UtcNow;

		var model = new TaskUpdateModel(
			request.Title,
			request.Description,
			request.RequirementsText,
			request.RewardPoints,
			coverImageId,
			request.ExecutionLocation,
			publishedAt,
			request.DeadlineAt,
			request.RegionName,
			request.SettlementName,
			request.TrustedCoordinatorIds?.ToArray() ?? Array.Empty<Guid>(),
			verificationType,
			reuseType,
			autoVerificationActionType);

		var result = await _tasks.UpdateAsync(actorUserId, actorUserPassword!, taskId, model, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	/// <summary>
	/// Закрывает задачу.
	/// </summary>
	/// <remarks>
	/// Операция доступна автору задачи или администратору.
	/// </remarks>
	/// <param name="taskId">Идентификатор задачи.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Задача успешно закрыта.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет прав на закрытие задачи.</response>
	/// <response code="404">Задача не найдена.</response>
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

	/// <summary>
	/// Открывает ранее закрытую задачу.
	/// </summary>
	/// <remarks>
	/// Операция доступна автору задачи или администратору.
	/// </remarks>
	/// <param name="taskId">Идентификатор задачи.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Задача успешно открыта.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет прав на открытие задачи.</response>
	/// <response code="404">Задача не найдена.</response>
	[HttpPost("{taskId:guid}/open")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> OpenAsync(
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

		var result = await _tasks.OpenAsync(actorUserId, actorUserPassword!, taskId, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	/// <summary>
	/// Возвращает публичную карточку задачи по идентификатору.
	/// </summary>
	/// <param name="taskId">Идентификатор задачи.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Задача найдена и успешно возвращена.</response>
	/// <response code="404">Задача не найдена.</response>
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

	/// <summary>
	/// Возвращает ленту задач для координатора или администратора.
	/// </summary>
	/// <remarks>
	/// Можно запрашивать только свои задачи, задачи по географии или общую доступную подборку.
	/// Поддерживаются фильтрация по статусу, сортировка по дате публикации и дедлайну, а также пагинация по диапазону.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, запрашивающего ленту.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="onlyMine">Если <see langword="true"/>, возвращаются только задачи автора/ответственного координатора.</param>
	/// <param name="regionName">Опциональный фильтр по региону.</param>
	/// <param name="settlementName">Опциональный фильтр по населённому пункту. Допустим только вместе с <c>regionName</c>.</param>
	/// <param name="status">Опциональный фильтр по статусу задачи: <c>open</c> или <c>closed</c>.</param>
	/// <param name="sort">Правило сортировки ленты.</param>
	/// <param name="includeExpiredDeadlines">Включать ли задачи с уже истёкшим дедлайном.</param>
	/// <param name="start">Начальный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="end">Конечный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Лента задач успешно возвращена.</response>
	/// <response code="400">Переданы некорректные фильтры или параметры пагинации.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет доступа к административной ленте.</response>
	[HttpGet("feed/coordinator")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> GetAdminFeedAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] bool onlyMine = true,
		[FromQuery] string? regionName = null,
		[FromQuery] string? settlementName = null,
		[FromQuery] string? status = null,
		[FromQuery] TaskFeedSort sort = TaskFeedSort.None,
		[FromQuery] bool includeExpiredDeadlines = false,
		[FromQuery] int? start = null,
		[FromQuery] int? end = null,
		CancellationToken cancellationToken = default)
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

		var invalidFilters = TryBuildFeedFilterValidationProblem(regionName, settlementName);
		if(invalidFilters is not null)
		{
			return invalidFilters;
		}

		if(!TryNormalizeTaskStatusFilter(status, out var normalizedStatusFilter, out var statusError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["status"] = new[] { statusError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр status допускает только значения 'open' или 'closed' (или пустое значение, чтобы не фильтровать).");
		}

		var invalidPagination = TryBuildFeedPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		var auth = await _tasks.ValidateActorAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!auth.IsSuccess)
		{
			return MapTaskError(auth.Error);
		}

		IEnumerable<TaskModel> tasks;

		if(onlyMine)
		{
			tasks = await _tasks.GetByCoordinatorAsync(actorUserId, cancellationToken);
		}
		else if(!string.IsNullOrWhiteSpace(regionName))
		{
			tasks = string.IsNullOrWhiteSpace(settlementName)
 				? await _tasks.GetByRegionAsync(regionName!, cancellationToken)
				: await _tasks.GetByRegionAndSettlementAsync(regionName!, settlementName!, cancellationToken);
		}
		else
		{
			var taskIds = await _taskFeed.GetAllTaskIdsAsync(cancellationToken);
			var list = new List<TaskModel>(taskIds.Count);

			for(var i = 0; i < taskIds.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var r = await _tasks.GetPublicAsync(taskIds[i], cancellationToken);
				if(r.IsSuccess && r.Value is not null)
				{
					list.Add(r.Value);
				}
			}

			tasks = list;
		}

		IEnumerable<TaskModel> filtered = tasks;

		if(!string.IsNullOrWhiteSpace(regionName))
		{
			filtered = string.IsNullOrWhiteSpace(settlementName)
 				? filtered.Where(t => string.Equals(t.RegionName, regionName, StringComparison.OrdinalIgnoreCase))
 				: filtered.Where(t =>
 					string.Equals(t.RegionName, regionName, StringComparison.OrdinalIgnoreCase)
					&& string.Equals(t.SettlementName, settlementName, StringComparison.OrdinalIgnoreCase));
		}

		if(normalizedStatusFilter is not null)
		{
			filtered = filtered.Where(t => string.Equals(
				NormalizeTaskStatusForContract(t),
				normalizedStatusFilter,
				StringComparison.Ordinal));
		}

		var nowUtc = DateTimeOffset.UtcNow;
		var ordered = ApplyDeadlineVisibilityAndSorting(filtered, sort, includeExpiredDeadlines, nowUtc);

		cancellationToken.ThrowIfCancellationRequested();

		var dtos = ordered.Select(ToDto).ToList();
		return Ok(ApplyFeedPagination(dtos, start, end));
	}

	/// <summary>
	/// Возвращает ленту задач, доступных текущему пользователю по его географии и правам.
	/// </summary>
	/// <remarks>
	/// В результирующую ленту попадают только открытые задачи.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, запрашивающего ленту.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="sort">Правило сортировки ленты.</param>
	/// <param name="includeExpiredDeadlines">Включать ли задачи с уже истёкшим дедлайном.</param>
	/// <param name="start">Начальный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="end">Конечный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Лента задач успешно возвращена.</response>
	/// <response code="400">Переданы некорректные параметры пагинации или сортировки.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
	[HttpGet("feed/user")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetUserFeedAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] TaskFeedSort sort = TaskFeedSort.None,
		[FromQuery] bool includeExpiredDeadlines = false,
		[FromQuery] int? start = null,
		[FromQuery] int? end = null,
		CancellationToken cancellationToken = default)
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

		var invalidPagination = TryBuildFeedPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		var auth = await _tasks.ValidateActorAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!auth.IsSuccess)
		{
			return MapTaskError(auth.Error);
		}

		var result = await _tasks.GetAvailableForUserAsync(actorUserId, cancellationToken);
		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		IEnumerable<TaskModel> filtered = result.Value;

		filtered = filtered.Where(t => string.Equals(
			NormalizeTaskStatusForContract(t),
			TaskStatus.Open,
			StringComparison.Ordinal));

		var nowUtc = DateTimeOffset.UtcNow;
		var ordered = ApplyDeadlineVisibilityAndSorting(filtered, sort, includeExpiredDeadlines, nowUtc);

		cancellationToken.ThrowIfCancellationRequested();

		var dtos = ordered.Select(ToDto).ToList();
		return Ok(ApplyFeedPagination(dtos, start, end));
	}

	/// <summary>
	/// Создаёт черновую заявку пользователя на выполнение задачи.
	/// </summary>
	/// <remarks>
	/// Пользователь может создавать заявку только от собственного имени, поэтому <c>userId</c>
	/// должен совпадать с <c>actorUserId</c>.
	/// Для задач с авто-подтверждением сервер может сразу завершить заявку автоматически.
	/// </remarks>
	/// <param name="taskId">Идентификатор задачи.</param>
	/// <param name="actorUserId">Идентификатор пользователя, отправляющего заявку.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="userId">Идентификатор пользователя, для которого создаётся заявка. Должен совпадать с <c>actorUserId</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="201">Заявка успешно создана или зафиксирована сервером.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Задача или пользователь не найдены.</response>
	/// <response code="409">Создание заявки недопустимо в текущем состоянии задачи или заявки.</response>
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
		[FromQuery] Guid userId,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var invalidTargetUser = this.TryBuildActorUserMatchValidationProblem(
			actorUserId,
			userId,
			nameof(userId));
		if(invalidTargetUser is not null) return invalidTargetUser;

		var model = new TaskSubmissionCreateModel(
			PhotoImageIds: null,
			ProofText: null,
			SubmittedAt: DateTimeOffset.UtcNow);

		var result = await _tasks.SubmitAsync(actorUserId, actorUserPassword!, userId, taskId, model, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapTaskError(result.Error);
		}

		return StatusCode(StatusCodes.Status201Created);
	}

	/// <summary>
	/// Отправляет ранее созданную заявку на ручную проверку координатором.
	/// </summary>
	/// <remarks>
	/// Тело запроса передаётся как <c>multipart/form-data</c>. Можно приложить текстовое доказательство
	/// и набор фотографий. Для задач с <c>verificationType=auto</c> эндпоинт недоступен.
	/// </remarks>
	/// <param name="submitId">Идентификатор заявки.</param>
	/// <param name="actorUserId">Идентификатор пользователя, владеющего заявкой.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Материалы заявки для ручной проверки.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Заявка успешно отправлена на проверку.</response>
	/// <response code="400">Переданы некорректные данные формы или файлов.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Заявка или задача не найдены.</response>
	/// <response code="409">Операция недопустима в текущем состоянии задачи или заявки.</response>
	[HttpPost("submit/{submitId:guid}/for-review")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> SubmitForReviewAsync(
		[FromRoute] Guid submitId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromForm] SubmitTaskFormRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null) return invalid;

		var auth = await _tasks.ValidateActorAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!auth.IsSuccess)
		{
			return MapTaskError(auth.Error);
		}

		IReadOnlyList<Guid>? photoImageIds = null;

		if(request.Photos is { Count: > 0 })
		{
			var models = new List<LdprActivistDemo.Application.Images.Models.ImageCreateModel>(request.Photos.Count);

			for(var i = 0; i < request.Photos.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var f = request.Photos[i];
				var err = UploadedImageReader.ValidateImage(f);
				if(err is not null)
				{
					return this.ValidationProblemWithCode(
						ApiErrorCodes.ValidationFailed,
						new Dictionary<string, string[]>
						{
							[$"photos[{i}]"] = new[] { err },
						});
				}

				models.Add(await UploadedImageReader.ReadAsync(f, cancellationToken));
			}

			photoImageIds = await _images.CreateManyAsync(actorUserId, models, cancellationToken);
		}

		var model = new TaskSubmissionCreateModel(
			PhotoImageIds: photoImageIds,
			ProofText: request.ProofText,
			SubmittedAt: DateTimeOffset.UtcNow);

		var result = await _tasks.SubmitForReviewAsync(actorUserId, actorUserPassword!, submitId, model, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	/// <summary>
	/// Обновляет содержимое уже существующей заявки.
	/// </summary>
	/// <remarks>
	/// Тело запроса передаётся как <c>multipart/form-data</c>. Для задач с <c>verificationType=auto</c>
	/// эндпоинт недоступен.
	/// </remarks>
	/// <param name="submitId">Идентификатор заявки.</param>
	/// <param name="actorUserId">Идентификатор пользователя, владеющего заявкой.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Обновлённые данные заявки.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Заявка успешно обновлена.</response>
	/// <response code="400">Переданы некорректные данные формы или файлов.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Заявка или задача не найдены.</response>
	/// <response code="409">Операция недопустима в текущем состоянии задачи или заявки.</response>
	[HttpPut("submit/{submitId:guid}")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UpdateSubmissionAsync(
		[FromRoute] Guid submitId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromForm] SubmitTaskFormRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null) return invalid;

		var auth = await _tasks.ValidateActorAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!auth.IsSuccess)
		{
			return MapTaskError(auth.Error);
		}

		IReadOnlyList<Guid>? photoImageIds = null;

		if(request.Photos is { Count: > 0 })
		{
			var models = new List<LdprActivistDemo.Application.Images.Models.ImageCreateModel>(request.Photos.Count);

			for(var i = 0; i < request.Photos.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var f = request.Photos[i];
				var err = UploadedImageReader.ValidateImage(f);
				if(err is not null)
				{
					return this.ValidationProblemWithCode(
						ApiErrorCodes.ValidationFailed,
						new Dictionary<string, string[]>
						{
							[$"photos[{i}]"] = new[] { err },
						});
				}

				models.Add(await UploadedImageReader.ReadAsync(f, cancellationToken));
			}

			photoImageIds = await _images.CreateManyAsync(actorUserId, models, cancellationToken);
		}

		var model = new TaskSubmissionCreateModel(
			PhotoImageIds: photoImageIds,
			ProofText: request.ProofText,
			SubmittedAt: DateTimeOffset.UtcNow);

		var result = await _tasks.UpdateSubmissionAsync(actorUserId, actorUserPassword!, submitId, model, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	/// <summary>
	/// Возвращает административную ленту заявок для координатора или администратора.
	/// </summary>
	/// <remarks>
	/// Поддерживаются фильтрация по статусу решения, задаче, пользователю и сортировка
	/// по времени отправки или принятия решения.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, запрашивающего ленту.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="status">Опциональный фильтр по статусу заявки: <c>in_progress</c>, <c>submitted_for_review</c>, <c>approve</c> или <c>rejected</c>.</param>
	/// <param name="taskId">Опциональный фильтр по задаче.</param>
	/// <param name="userId">Опциональный фильтр по пользователю.</param>
	/// <param name="sort">Правило сортировки ленты.</param>
	/// <param name="start">Начальный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="end">Конечный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Лента заявок успешно возвращена.</response>
	/// <response code="400">Переданы некорректные фильтры или параметры пагинации.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет доступа к административной ленте заявок.</response>
	/// <response code="404">Связанный объект не найден.</response>
	[HttpGet("submit/feed/coordinator")]
	[ProducesResponseType(typeof(IReadOnlyList<SubmissionDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetSubmissionAdminFeedAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] string? status = null,
		[FromQuery] Guid? taskId = null,
		[FromQuery] Guid? userId = null,
		[FromQuery] SubmissionFeedSort sort = SubmissionFeedSort.None,
		[FromQuery] int? start = null,
		[FromQuery] int? end = null,
		CancellationToken cancellationToken = default)
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

		var invalidFilters = TryBuildSubmissionFeedFilterValidationProblem(taskId, userId);
		if(invalidFilters is not null)
		{
			return invalidFilters;
		}

		if(!TryNormalizeSubmissionDecisionStatusFilter(status, out var normalizedStatus, out var statusError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["status"] = new[] { statusError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр status допускает только значения 'in_progress', 'submitted_for_review', 'approve' или 'rejected' (или пустое значение, чтобы не фильтровать).");
		}

		var invalidPagination = TryBuildFeedPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		var result = await _tasks.GetSubmissionCoordinatorFeedAsync(
			actorUserId,
			actorUserPassword!,
			taskId,
			userId,
			normalizedStatus,
			cancellationToken);

		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		var ordered = ApplySubmissionSorting(result.Value, sort);
		cancellationToken.ThrowIfCancellationRequested();

		var dtos = ordered.Select(ToDto).ToList();
		return Ok(ApplyFeedPagination(dtos, start, end));
	}

	/// <summary>
	/// Возвращает пользовательскую ленту собственных заявок.
	/// </summary>
	/// <remarks>
	/// Пользователь может запрашивать только свои заявки, поэтому <c>userId</c> должен совпадать с <c>actorUserId</c>.
	/// </remarks>
	/// <param name="actorUserId">Идентификатор пользователя, запрашивающего ленту.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="taskId">Опциональный фильтр по задаче.</param>
	/// <param name="userId">Идентификатор пользователя-владельца заявок. Должен совпадать с <c>actorUserId</c>.</param>
	/// <param name="status">Опциональный фильтр по статусу заявки: <c>in_progress</c>, <c>submitted_for_review</c>, <c>approve</c> или <c>rejected</c>.</param>
	/// <param name="sort">Правило сортировки ленты.</param>
	/// <param name="start">Начальный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="end">Конечный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Лента заявок успешно возвращена.</response>
	/// <response code="400">Переданы некорректные фильтры или параметры пагинации.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Связанный объект не найден.</response>
	[HttpGet("submit/feed/user")]
	[ProducesResponseType(typeof(IReadOnlyList<SubmissionDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetSubmissionUserFeedAsync(
		 [FromQuery] Guid actorUserId,
		 [FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		 [FromQuery] Guid? taskId,
		 [FromQuery] Guid userId,
		 [FromQuery] string? status = null,
		 [FromQuery] SubmissionFeedSort sort = SubmissionFeedSort.None,
		 [FromQuery] int? start = null,
		 [FromQuery] int? end = null,
		 CancellationToken cancellationToken = default)
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

		var invalidFilters = TryBuildSubmissionFeedFilterValidationProblem(taskId, userId);
		if(invalidFilters is not null)
		{
			return invalidFilters;
		}

		var invalidTargetUser = this.TryBuildActorUserMatchValidationProblem(actorUserId, userId, nameof(userId));
		if(invalidTargetUser is not null)
		{
			return invalidTargetUser;
		}

		if(!TryNormalizeSubmissionDecisionStatusFilter(status, out var normalizedStatus, out var statusError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["status"] = new[] { statusError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр status допускает только значения 'in_progress', 'submitted_for_review', 'approve' или 'rejected' (или пустое значение, чтобы не фильтровать).");
		}

		var invalidPagination = TryBuildFeedPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		var result = await _tasks.GetSubmissionUserFeedAsync(
			actorUserId,
			actorUserPassword!,
			taskId,
			userId,
			normalizedStatus,
			cancellationToken);

		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		var ordered = ApplySubmissionSorting(result.Value, sort);
		cancellationToken.ThrowIfCancellationRequested();

		var dtos = ordered.Select(ToDto).ToList();
		return Ok(ApplyFeedPagination(dtos, start, end));
	}

	/// <summary>
	/// Возвращает подробную информацию о заявке по её идентификатору.
	/// </summary>
	/// <remarks>
	/// Доступ зависит от роли пользователя и его отношения к заявке и задаче.
	/// </remarks>
	/// <param name="submitId">Идентификатор заявки.</param>
	/// <param name="actorUserId">Идентификатор пользователя, запрашивающего заявку.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Заявка успешно возвращена.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет доступа к заявке.</response>
	/// <response code="404">Заявка не найдена.</response>
	[HttpGet("submit/{submitId:guid}")]
	[ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetSubmissionByIdAsync(
		[FromRoute] Guid submitId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _tasks.GetSubmissionByIdAsync(actorUserId, actorUserPassword!, submitId, cancellationToken);
		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		return Ok(ToDto(result.Value));
	}

	/// <summary>
	/// Возвращает пользователей, связанных с задачей, либо количество таких пользователей.
	/// </summary>
	/// <remarks>
	/// Если <c>taskStatus</c> не указан, возвращаются пользователи подходящей географии,
	/// которые ещё не создали заявку по задаче. Если указан статус, выбираются пользователи
	/// по состоянию их заявок. Формат ответа задаётся параметром <c>responseFormat</c>.
	/// </remarks>
	/// <param name="taskId">Идентификатор задачи.</param>
	/// <param name="actorUserId">Идентификатор пользователя, запрашивающего данные.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="taskStatus">Опциональный фильтр по состоянию заявки: <c>in_progress</c>, <c>submitted_for_review</c>, <c>approve</c> или <c>rejected</c>.</param>
	/// <param name="responseFormat">Формат ответа: список пользователей или только количество.</param>
	/// <param name="start">Начальный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="end">Конечный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Информация о пользователях успешно возвращена.</response>
	/// <response code="400">Переданы некорректные параметры фильтрации или пагинации.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет доступа к задаче.</response>
	/// <response code="404">Задача не найдена.</response>
	[HttpGet("{taskId:guid}/feed/users")]
	[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(UsersCountResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetTaskUsersAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] string? taskStatus = null,
		[FromQuery] TaskUsersResponseFormat responseFormat = TaskUsersResponseFormat.Users,
		[FromQuery] int? start = null,
		[FromQuery] int? end = null,
		CancellationToken cancellationToken = default)
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

		if(taskId == Guid.Empty)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["taskId"] = new[] { "TaskId must be non-empty GUID." },
				},
				title: "Некорректный запрос.",
				detail: "Передайте корректный taskId.");
		}

		if(!TryNormalizeSubmissionDecisionStatusFilter(taskStatus, out var normalizedTaskStatus, out var taskStatusError))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["taskStatus"] = new[] { taskStatusError! },
				},
				title: "Некорректный запрос.",
				detail: "Параметр taskStatus допускает только значения 'in_progress', 'submitted_for_review', 'approve' или 'rejected'. Пустое значение включает режим поиска пользователей без заявки к задаче по географии задачи.");
		}

		var normalizedResponseFormat = responseFormat switch
		{
			TaskUsersResponseFormat.Count => UserResponseFormat.Count,
			_ => UserResponseFormat.Users,
		};

		var invalidPagination = TryBuildFeedPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		var result = await _tasks.GetTaskUsersAsync(
			actorUserId,
			actorUserPassword!,
			taskId,
			normalizedTaskStatus,
			cancellationToken);

		if(!result.IsSuccess || result.Value is null)
		{
			return MapTaskError(result.Error);
		}

		if(string.Equals(normalizedResponseFormat, UserResponseFormat.Count, StringComparison.Ordinal))
		{
			return Ok(new UsersCountResponse(result.Value.Count));
		}

		cancellationToken.ThrowIfCancellationRequested();

		var dtos = result.Value.Select(ToUserDto).ToList();
		return Ok(ApplyFeedPagination(dtos, start, end));
	}

	/// <summary>
	/// Одобряет заявку пользователя.
	/// </summary>
	/// <remarks>
	/// Операция доступна автору задачи, доверенному координатору задачи или администратору.
	/// При успешном одобрении пользователю может быть начислена награда задачи.
	/// </remarks>
	/// <param name="submitId">Идентификатор заявки.</param>
	/// <param name="actorUserId">Идентификатор пользователя, принимающего решение.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Заявка успешно одобрена.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет прав на одобрение заявки.</response>
	/// <response code="404">Заявка или задача не найдены.</response>
	/// <response code="409">Операция недопустима в текущем состоянии задачи или заявки.</response>
	[HttpPost("submit/{submitId:guid}/approve")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ApproveAsync(
		[FromRoute] Guid submitId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _tasks.ApproveAsync(actorUserId, actorUserPassword!, submitId, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	/// <summary>
	/// Отклоняет заявку пользователя.
	/// </summary>
	/// <remarks>
	/// Операция доступна автору задачи, доверенному координатору задачи или администратору.
	/// </remarks>
	/// <param name="submitId">Идентификатор заявки.</param>
	/// <param name="actorUserId">Идентификатор пользователя, принимающего решение.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Заявка успешно отклонена.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Пользователь не имеет прав на отклонение заявки.</response>
	/// <response code="404">Заявка или задача не найдены.</response>
	/// <response code="409">Операция недопустима в текущем состоянии задачи или заявки.</response>
	[HttpPost("submit/{submitId:guid}/reject")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> RejectAsync(
		[FromRoute] Guid submitId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _tasks.RejectAsync(actorUserId, actorUserPassword!, submitId, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	/// <summary>
	/// Модель <c>multipart/form-data</c> для создания задачи.
	/// </summary>
	public sealed class CreateTaskFormRequest
	{
		/// <summary>
		/// Заголовок задачи.
		/// </summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>
		/// Подробное описание задачи.
		/// </summary>
		public string Description { get; set; } = string.Empty;

		/// <summary>
		/// Дополнительные требования к выполнению задачи.
		/// </summary>
		public string? RequirementsText { get; set; }

		/// <summary>
		/// Награда за выполнение задачи в баллах.
		/// </summary>
		public int RewardPoints { get; set; }

		/// <summary>
		/// Тип верификации задачи: <c>manual</c> или <c>auto</c>.
		/// </summary>
		public string? VerificationType { get; set; }

		/// <summary>
		/// Тип повторного использования задачи: <c>disposable</c> или <c>reusable</c>.
		/// </summary>
		public string? ReuseType { get; set; }

		/// <summary>
		/// Действие для авто-верификации: <c>invite_friend</c>, <c>first_login</c> или <c>auto</c>.
		/// </summary>
		public string? AutoVerificationActionType { get; set; }

		/// <summary>
		/// Файл обложки задачи.
		/// </summary>
		public IFormFile? CoverImage { get; set; }

		/// <summary>
		/// Произвольное текстовое описание места выполнения задачи.
		/// </summary>
		public string? ExecutionLocation { get; set; }

		/// <summary>
		/// Дедлайн выполнения задачи в UTC.
		/// </summary>
		public DateTimeOffset? DeadlineAt { get; set; }

		/// <summary>
		/// Название региона выполнения задачи.
		/// </summary>
		public string RegionName { get; set; } = string.Empty;

		/// <summary>
		/// Название населённого пункта выполнения задачи. Может быть пустым для задач уровня региона.
		/// </summary>
		public string? SettlementName { get; set; }

		/// <summary>
		/// Идентификаторы доверенных координаторов задачи.
		/// </summary>
		public List<Guid>? TrustedCoordinatorIds { get; set; }
	}

	/// <summary>
	/// Модель <c>multipart/form-data</c> для обновления задачи.
	/// </summary>
	public sealed class UpdateTaskFormRequest
	{
		/// <summary>
		/// Заголовок задачи.
		/// </summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>
		/// Подробное описание задачи.
		/// </summary>
		public string Description { get; set; } = string.Empty;

		/// <summary>
		/// Дополнительные требования к выполнению задачи.
		/// </summary>
		public string? RequirementsText { get; set; }

		/// <summary>
		/// Награда за выполнение задачи в баллах.
		/// </summary>
		public int RewardPoints { get; set; }

		/// <summary>
		/// Новый тип верификации задачи: <c>manual</c> или <c>auto</c>.
		/// </summary>
		public string? VerificationType { get; set; }

		/// <summary>
		/// Новый тип повторного использования задачи: <c>disposable</c> или <c>reusable</c>.
		/// </summary>
		public string? ReuseType { get; set; }

		/// <summary>
		/// Новое действие для авто-верификации: <c>invite_friend</c>, <c>first_login</c> или <c>auto</c>.
		/// </summary>
		public string? AutoVerificationActionType { get; set; }

		/// <summary>
		/// Новый файл обложки задачи.
		/// </summary>
		public IFormFile? CoverImage { get; set; }

		/// <summary>
		/// Произвольное текстовое описание места выполнения задачи.
		/// </summary>
		public string? ExecutionLocation { get; set; }

		/// <summary>
		/// Новый дедлайн выполнения задачи в UTC.
		/// </summary>
		public DateTimeOffset? DeadlineAt { get; set; }

		/// <summary>
		/// Название региона выполнения задачи.
		/// </summary>
		public string RegionName { get; set; } = string.Empty;

		/// <summary>
		/// Название населённого пункта выполнения задачи. Может быть пустым для задач уровня региона.
		/// </summary>
		public string? SettlementName { get; set; }

		/// <summary>
		/// Идентификаторы доверенных координаторов задачи.
		/// </summary>
		public List<Guid>? TrustedCoordinatorIds { get; set; }
	}

	/// <summary>
	/// Модель <c>multipart/form-data</c> для отправки или обновления материалов заявки.
	/// </summary>
	public sealed class SubmitTaskFormRequest
	{
		/// <summary>
		/// Текстовое доказательство выполнения задачи.
		/// </summary>
		public string? ProofText { get; set; }

		/// <summary>
		/// Фотографии, подтверждающие выполнение задачи.
		/// </summary>
		public List<IFormFile>? Photos { get; set; }
	}

	private static IReadOnlyList<TaskSubmissionModel> ApplySubmissionSorting(
		IEnumerable<TaskSubmissionModel> submissions,
		SubmissionFeedSort sort)
	{
		var list = submissions.ToList();

		switch(sort)
		{
			case SubmissionFeedSort.SubmittedNewest:
				return list
					.OrderByDescending(s => s.SubmittedAt)
					.ThenBy(s => s.Id)
					.ToList();

			case SubmissionFeedSort.SubmittedOldest:
				return list
					.OrderBy(s => s.SubmittedAt)
					.ThenBy(s => s.Id)
					.ToList();

			case SubmissionFeedSort.DecidedNewest:
				return list
					.OrderByDescending(s => s.DecidedAt ?? DateTimeOffset.MinValue)
					.ThenBy(s => s.Id)
					.ToList();

			case SubmissionFeedSort.DecidedOldest:
				return list
					.OrderBy(s => s.DecidedAt ?? DateTimeOffset.MaxValue)
					.ThenBy(s => s.Id)
					.ToList();

			case SubmissionFeedSort.None:
			default:
				return list;
		}
	}

	private IActionResult? TryBuildActorValidationProblem(Guid actorUserId, string? actorUserPassword)
		=> this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);

	private IActionResult? TryBuildFeedFilterValidationProblem(string? regionName, string? settlementName)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(regionName is not null && string.IsNullOrWhiteSpace(regionName))
		{
			errors["regionName"] = new[] { "RegionName must not be empty." };
		}

		if(settlementName is not null)
		{
			var list = new List<string>();

			if(string.IsNullOrWhiteSpace(settlementName))
			{
				list.Add("SettlementName must not be empty.");
			}

			if(string.IsNullOrWhiteSpace(regionName))
			{
				list.Add("settlementName can be used only together with regionName.");
			}

			if(list.Count > 0)
			{
				errors["settlementName"] = list.ToArray();
			}
		}

		return errors.Count == 0
			? null
			: this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
			   detail: "Проверьте параметры regionName и settlementName (settlementName допускается только вместе с regionName).");
	}

	private IActionResult? TryBuildFeedPaginationValidationProblem(int? start, int? end)
	{
		if(start is null && end is null)
		{
			return null;
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(start is null || end is null)
		{
			if(start is null)
			{
				errors["start"] = new[] { "Start is required when end is specified." };
			}

			if(end is null)
			{
				errors["end"] = new[] { "End is required when start is specified." };
			}

			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Параметры start и end должны быть указаны вместе.");
		}

		if(start.Value <= 0)
		{
			errors["start"] = new[] { "Start must be positive." };
		}

		if(end.Value <= 0)
		{
			errors["end"] = new[] { "End must be positive." };
		}

		if(errors.Count == 0 && end.Value < start.Value)
		{
			errors["end"] = new[] { "End must be greater or equal to start." };
		}

		return errors.Count == 0
			? null
			: this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте параметры start и end (нумерация с 1, end должен быть >= start).");
	}

	private static IReadOnlyList<T> ApplyFeedPagination<T>(IReadOnlyList<T> items, int? start, int? end)
	{
		if(start is null || end is null)
		{
			return items;
		}

		var skip = start.Value - 1;
		var take = end.Value - start.Value + 1;

		if(skip < 0 || take <= 0)
		{
			return Array.Empty<T>();
		}

		return items
			.Skip(skip)
			.Take(take)
			.ToList();
	}

	private IActionResult? TryBuildSubmissionFeedFilterValidationProblem(Guid? taskId, Guid? userId)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(taskId.HasValue && taskId.Value == Guid.Empty)
		{
			errors["taskId"] = new[] { "TaskId must be non-empty GUID." };
		}

		if(userId.HasValue && userId.Value == Guid.Empty)
		{
			errors["userId"] = new[] { "UserId must be non-empty GUID." };
		}

		return errors.Count == 0
			? null
			: this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте параметры taskId и userId (GUID не должен быть пустым).");
	}

	private static bool TryNormalizeSubmissionDecisionStatusFilter(string? raw, out string? normalized, out string? error)
	{
		error = null;
		normalized = null;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return true;
		}

		if(string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if(string.Equals(raw, TaskSubmissionDecisionStatus.InProgress, StringComparison.OrdinalIgnoreCase))
		{
			normalized = TaskSubmissionDecisionStatus.InProgress;
			return true;
		}

		if(string.Equals(raw, TaskSubmissionDecisionStatus.SubmittedForReview, StringComparison.OrdinalIgnoreCase))
		{
			normalized = TaskSubmissionDecisionStatus.SubmittedForReview;
			return true;
		}

		if(string.Equals(raw, TaskSubmissionDecisionStatus.Approve, StringComparison.OrdinalIgnoreCase))
		{
			normalized = TaskSubmissionDecisionStatus.Approve;
			return true;
		}

		if(string.Equals(raw, TaskSubmissionDecisionStatus.Rejected, StringComparison.OrdinalIgnoreCase))
		{
			normalized = TaskSubmissionDecisionStatus.Rejected;
			return true;
		}

		error = "Status must be 'in_progress', 'submitted_for_review', 'approve' or 'rejected' (or be empty).";
		return false;
	}

	private static bool TryNormalizeTaskVerificationTypeForCreate(string? raw, out string normalized, out string? error)
	{
		error = null;
		normalized = string.Empty;

		if(string.IsNullOrWhiteSpace(raw))
		{
			error = "VerificationType must be 'auto' or 'manual'.";
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Auto;
			return true;
		}

		if(string.Equals(token, TaskVerificationType.Manual, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Manual;
			return true;
		}

		error = "VerificationType must be 'auto' or 'manual'.";
		return false;
	}

	private static bool TryNormalizeTaskReuseTypeForCreate(string? raw, out string normalized, out string? error)
	{
		error = null;
		normalized = string.Empty;

		if(string.IsNullOrWhiteSpace(raw))
		{
			error = "ReuseType must be 'disposable' or 'reusable'.";
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskReuseType.Disposable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Disposable;
			return true;
		}

		if(string.Equals(token, TaskReuseType.Reusable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Reusable;
			return true;
		}

		error = "ReuseType must be 'disposable' or 'reusable'.";
		return false;
	}

	private static bool TryNormalizeTaskReuseTypeForUpdate(string? raw, out string? normalized, out string? error)
	{
		error = null;
		normalized = string.Empty;

		if(string.IsNullOrWhiteSpace(raw))
		{
			error = "ReuseType must be 'disposable' or 'reusable'.";
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskReuseType.Disposable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Disposable;
			return true;
		}

		if(string.Equals(token, TaskReuseType.Reusable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Reusable;
			return true;
		}

		error = "ReuseType must be 'disposable' or 'reusable'.";
		return false;
	}

	private static bool TryNormalizeTaskAutoVerificationActionTypeForCreate(
		string? raw,
		string normalizedVerificationType,
		out string? normalized,
		out string? error)
	{
		error = null;
		normalized = null;

		if(string.Equals(normalizedVerificationType, TaskVerificationType.Manual, StringComparison.Ordinal))
		{
			return true;
		}

		if(string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			error = "AutoVerificationActionType is required when verificationType is 'auto'.";
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskAutoVerificationActionType.InviteFriend, StringComparison.Ordinal))
		{
			normalized = TaskAutoVerificationActionType.InviteFriend;
			return true;
		}

		if(string.Equals(token, TaskAutoVerificationActionType.FirstLogin, StringComparison.Ordinal))
		{
			normalized = TaskAutoVerificationActionType.FirstLogin;
			return true;
		}

		if(string.Equals(token, TaskAutoVerificationActionType.Auto, StringComparison.Ordinal))
		{
			normalized = TaskAutoVerificationActionType.Auto;
			return true;
		}

		error = $"AutoVerificationActionType must be '{TaskAutoVerificationActionType.InviteFriend}', '{TaskAutoVerificationActionType.FirstLogin}' or '{TaskAutoVerificationActionType.Auto}'.";
		return false;
	}

	private static bool TryNormalizeTaskAutoVerificationActionTypeForUpdate(
		string? raw,
		string? normalizedVerificationType,
		out string? normalized,
		out string? error)
	{
		error = null;
		normalized = null;

		if(string.Equals(normalizedVerificationType, TaskVerificationType.Manual, StringComparison.Ordinal))
		{
			return true;
		}

		if(string.Equals(normalizedVerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			if(string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
			{
				error = "AutoVerificationActionType is required when verificationType is set to 'auto'.";
				return false;
			}
		}

		if(string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskAutoVerificationActionType.InviteFriend, StringComparison.Ordinal))
		{
			normalized = TaskAutoVerificationActionType.InviteFriend;
			return true;
		}

		if(string.Equals(token, TaskAutoVerificationActionType.FirstLogin, StringComparison.Ordinal))
		{
			normalized = TaskAutoVerificationActionType.FirstLogin;
			return true;
		}

		if(string.Equals(token, TaskAutoVerificationActionType.Auto, StringComparison.Ordinal))
		{
			normalized = TaskAutoVerificationActionType.Auto;
			return true;
		}

		error = $"AutoVerificationActionType must be '{TaskAutoVerificationActionType.InviteFriend}', '{TaskAutoVerificationActionType.FirstLogin}' or '{TaskAutoVerificationActionType.Auto}'.";
		return false;
	}

	private static bool TryNormalizeTaskVerificationTypeForUpdate(string? raw, out string? normalized, out string? error)
	{
		error = null;
		normalized = null;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return true;
		}

		if(string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Auto;
			return true;
		}

		if(string.Equals(token, TaskVerificationType.Manual, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Manual;
			return true;
		}

		error = "VerificationType must be 'auto' or 'manual' (or be empty).";
		return false;
	}

	private static bool TryNormalizeTaskStatusFilter(string? raw, out string? normalized, out string? error)
	{
		error = null;
		normalized = null;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return true;
		}

		if(string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if(string.Equals(raw, TaskStatus.Open, StringComparison.OrdinalIgnoreCase))
		{
			normalized = TaskStatus.Open;
			return true;
		}

		if(string.Equals(raw, TaskStatus.Closed, StringComparison.OrdinalIgnoreCase))
		{
			normalized = TaskStatus.Closed;
			return true;
		}

		error = "Status must be 'open' or 'closed' (or be empty).";
		return false;
	}

	private static string NormalizeTaskStatusForContract(TaskModel t)
	{
		var s = t.Status.ToString();

		if(string.Equals(s, TaskStatus.Open, StringComparison.OrdinalIgnoreCase))
		{
			return TaskStatus.Open;
		}

		if(string.Equals(s, TaskStatus.Closed, StringComparison.OrdinalIgnoreCase))
		{
			return TaskStatus.Closed;
		}

		return s;
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
			TaskOperationError.Forbidden => this.ProblemWithCode(StatusCodes.Status403Forbidden, ApiErrorCodes.Forbidden, "Нет доступа.", "Операция запрещена: требуется автор задачи или назначенный ответственный координатор/администратор."),
			TaskOperationError.TaskAccessDenied => this.ProblemWithCode(StatusCodes.Status403Forbidden, ApiErrorCodes.TaskAccessDenied, "Нет доступа.", "Задача недоступна пользователю или операция запрещена для текущей роли."),
			TaskOperationError.TaskNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.TaskNotFound, "Задача не найдена."),
			TaskOperationError.RegionNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.GeoRegionNotFound, "Регион не найден."),
			TaskOperationError.SettlementRegionMismatch => this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.SettlementRegionMismatch, "Населённый пункт не принадлежит региону.", "Указанный населённый пункт должен относиться к указанному региону."),
			TaskOperationError.TaskClosed => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskClosed, "Задача закрыта.", "Операция недоступна для закрытой задачи."),
			TaskOperationError.TaskAutoVerificationNotSupported => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskAutoVerificationNotSupported, "Операция недоступна.", "Операции отправки/изменения/удаления заявки и ручной модерации недоступны для задач с VerificationType='auto'."),
			TaskOperationError.AlreadySubmitted => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskAlreadySubmitted, "Заявка уже подтверждена.", "Нельзя изменять подтверждённую заявку."),
			TaskOperationError.SubmissionAlreadyExists => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskSubmissionExists, "Заявка уже существует.", "Повторная отправка запрещена."),
			TaskOperationError.SubmissionNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.TaskSubmissionNotFound, "Заявка не найдена."),
			TaskOperationError.UserNotFound => this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.", "Пользователь не существует или один из TrustedCoordinatorIds не имеет роли coordinator/admin."),
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
			t.CoverImageId,
			t.ExecutionLocation,
			t.PublishedAt,
			t.DeadlineAt,
			NormalizeTaskStatusForContract(t),
			t.RegionName,
			t.SettlementName,
			t.TrustedCoordinatorIds,
			t.VerificationType,
			t.ReuseType,
			t.AutoVerificationActionType);

	private static SubmissionDto ToDto(TaskSubmissionModel s)
		=> new(
			s.Id,
			s.TaskId,
			s.UserId,
			s.SubmittedAt,
			s.DecisionStatus,
			s.DecidedByCoordinatorId,
			s.DecidedAt,
			s.PhotoImageIds,
			s.ProofText);

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