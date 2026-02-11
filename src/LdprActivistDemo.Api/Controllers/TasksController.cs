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

namespace LdprActivistDemo.Api.Controllers;

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
			coverImageId = await _images.CreateAsync(img, cancellationToken);
		}

		var model = new TaskCreateModel(
			request.Title,
			request.Description,
			request.RequirementsText,
			request.RewardPoints,
			coverImageId,
			request.ExecutionLocation,
			request.PublishedAt,
			request.DeadlineAt,
			request.Status,
			request.RegionId,
			request.CityId,
			request.TrustedAdminIds?.ToArray() ?? Array.Empty<Guid>());

		var result = await _tasks.CreateAsync(actorUserId, actorUserPassword!, model, cancellationToken);
		if(!result.IsSuccess)
		{
			return MapTaskError(result.Error);
		}

		return Created($"/api/v1/tasks/{result.Value}", new CreateTaskResponse(result.Value));
	}

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
			coverImageId = await _images.CreateAsync(img, cancellationToken);
		}

		var model = new TaskUpdateModel(
			request.Title,
			request.Description,
			request.RequirementsText,
			request.RewardPoints,
			coverImageId,
			request.ExecutionLocation,
			request.PublishedAt,
			request.DeadlineAt,
			request.Status,
			request.RegionId,
			request.CityId,
			request.TrustedAdminIds?.ToArray() ?? Array.Empty<Guid>());

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

	[HttpGet("feed/admin")]
	[ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> GetAdminFeedAsync(
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromQuery] bool onlyMine = true,
		[FromQuery] int? regionId = null,
		[FromQuery] int? cityId = null,
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

		var invalidFilters = TryBuildFeedFilterValidationProblem(regionId, cityId);
		if(invalidFilters is not null)
		{
			return invalidFilters;
		}

		var invalidPagination = TryBuildFeedPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		var probe = await _tasks.DeleteAsync(actorUserId, actorUserPassword!, Guid.NewGuid(), cancellationToken);
		if(!probe.IsSuccess && (probe.Error == TaskOperationError.InvalidCredentials || probe.Error == TaskOperationError.Forbidden))
		{
			return MapTaskError(probe.Error);
		}

		IEnumerable<TaskModel> tasks;

		if(onlyMine)
		{
			tasks = await _tasks.GetByAdminAsync(actorUserId, cancellationToken);
		}
		else if(regionId is not null)
		{
			tasks = cityId is null
				? await _tasks.GetByRegionAsync(regionId.Value, cancellationToken)
				: await _tasks.GetByRegionAndCityAsync(regionId.Value, cityId.Value, cancellationToken);
		}
		else
		{
			var taskIds = await _taskFeed.GetAllTaskIdsAsync(cancellationToken);
			var list = new List<TaskModel>(taskIds.Count);

			for(var i = 0; i < taskIds.Count; i++)
			{
				var r = await _tasks.GetPublicAsync(taskIds[i], cancellationToken);
				if(r.IsSuccess && r.Value is not null)
				{
					list.Add(r.Value);
				}
			}

			tasks = list;
		}

		IEnumerable<TaskModel> filtered = tasks;

		if(regionId is not null)
		{
			filtered = cityId is null
				? filtered.Where(t => t.RegionId == regionId.Value)
				: filtered.Where(t => t.RegionId == regionId.Value && t.CityId == cityId.Value);
		}

		var nowUtc = DateTimeOffset.UtcNow;
		var ordered = ApplyDeadlineVisibilityAndSorting(filtered, sort, includeExpiredDeadlines, nowUtc);

		var dtos = ordered.Select(ToDto).ToList();
		return Ok(ApplyFeedPagination(dtos, start, end));
	}

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

		var auth = await _tasks.GetByUserSubmittedAsync(actorUserId, actorUserPassword!, cancellationToken);
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

		var nowUtc = DateTimeOffset.UtcNow;
		var ordered = ApplyDeadlineVisibilityAndSorting(filtered, sort, includeExpiredDeadlines, nowUtc);

		var dtos = ordered.Select(ToDto).ToList();
		return Ok(ApplyFeedPagination(dtos, start, end));
	}

	[HttpPost("{taskId:guid}/submit")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> SubmitAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromForm] SubmitTaskFormRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null)
		{
			return invalid;
		}

		IReadOnlyList<Guid>? photoImageIds = null;

		if(request.Photos is { Count: > 0 })
		{
			var models = new List<LdprActivistDemo.Application.Images.Models.ImageCreateModel>(request.Photos.Count);

			for(var i = 0; i < request.Photos.Count; i++)
			{
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

			photoImageIds = await _images.CreateManyAsync(models, cancellationToken);
		}

		var model = new TaskSubmissionCreateModel(
			PhotoImageIds: photoImageIds,
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
	[Consumes("multipart/form-data")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UpdateSubmissionAsync(
		[FromRoute] Guid taskId,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromForm] SubmitTaskFormRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = TryBuildActorValidationProblem(actorUserId, actorUserPassword);
		if(invalidActor is not null) return invalidActor;

		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null) return invalid;

		IReadOnlyList<Guid>? photoImageIds = null;

		if(request.Photos is { Count: > 0 })
		{
			var models = new List<LdprActivistDemo.Application.Images.Models.ImageCreateModel>(request.Photos.Count);

			for(var i = 0; i < request.Photos.Count; i++)
			{
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

			photoImageIds = await _images.CreateManyAsync(models, cancellationToken);
		}

		var model = new TaskSubmissionCreateModel(
			PhotoImageIds: photoImageIds,
			ProofText: request.ProofText,
			SubmittedAt: request.SubmittedAt == default ? DateTimeOffset.UtcNow : request.SubmittedAt);

		var result = await _tasks.UpdateSubmissionAsync(actorUserId, actorUserPassword!, taskId, model, cancellationToken);
		return result.IsSuccess ? NoContent() : MapTaskError(result.Error);
	}

	public sealed class CreateTaskFormRequest
	{
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string? RequirementsText { get; set; }
		public int RewardPoints { get; set; }

		public IFormFile? CoverImage { get; set; }

		public string? ExecutionLocation { get; set; }
		public DateTimeOffset PublishedAt { get; set; }
		public DateTimeOffset? DeadlineAt { get; set; }
		public LdprActivistDemo.Contracts.Tasks.TaskStatus Status { get; set; }
		public int RegionId { get; set; }
		public int? CityId { get; set; }
		public List<Guid>? TrustedAdminIds { get; set; }
	}

	public sealed class UpdateTaskFormRequest
	{
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string? RequirementsText { get; set; }
		public int RewardPoints { get; set; }

		public IFormFile? CoverImage { get; set; }

		public string? ExecutionLocation { get; set; }
		public DateTimeOffset PublishedAt { get; set; }
		public DateTimeOffset? DeadlineAt { get; set; }
		public LdprActivistDemo.Contracts.Tasks.TaskStatus Status { get; set; }
		public int RegionId { get; set; }
		public int? CityId { get; set; }
		public List<Guid>? TrustedAdminIds { get; set; }
	}

	public sealed class SubmitTaskFormRequest
	{
		public DateTimeOffset SubmittedAt { get; set; }
		public string? ProofText { get; set; }

		public List<IFormFile>? Photos { get; set; }
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

	[HttpPost("{taskId:guid}/rejected")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> RejectAsync(
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

		var result = await _tasks.RejectAsync(actorUserId, actorUserPassword!, taskId, userId, cancellationToken);
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

	private IActionResult? TryBuildFeedFilterValidationProblem(int? regionId, int? cityId)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(regionId is not null && regionId.Value <= 0)
		{
			errors["regionId"] = new[] { "RegionId must be positive." };
		}

		if(cityId is not null)
		{
			var list = new List<string>();

			if(cityId.Value <= 0)
			{
				list.Add("CityId must be positive.");
			}

			if(regionId is null)
			{
				list.Add("cityId can be used only together with regionId.");
			}

			if(list.Count > 0)
			{
				errors["cityId"] = list.ToArray();
			}
		}

		return errors.Count == 0
			? null
			: this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте параметры regionId и cityId (cityId допускается только вместе с regionId).");
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
			TaskOperationError.AlreadySubmitted => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.TaskAlreadySubmitted, "Заявка уже подтверждена.", "Нельзя изменять подтверждённую заявку."),
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
			t.CoverImageId,
			t.ExecutionLocation,
			t.PublishedAt,
			t.DeadlineAt,
			t.Status,
			t.RegionId,
			t.CityId,
			t.TrustedAdminIds);

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
				u.Points)
		{
			AvatarImageUrl = u.AvatarImageUrl,
		};

	private static SubmissionDto ToDto(TaskSubmissionModel s)
		=> new(
			s.Id,
			s.TaskId,
			s.UserId,
			s.SubmittedAt,
			s.DecisionStatus,
			s.DecidedByAdminId,
			s.DecidedAt,
			s.PhotoImageIds,
			s.ProofText);
}