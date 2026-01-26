namespace LdprActivistDemo.Application.Tasks.Models;

public enum TaskOperationError
{
	None = 0,
	ValidationFailed,
	InvalidCredentials,
	Forbidden,
	TaskNotFound,
	RegionNotFound,
	CityRegionMismatch,
	TaskClosed,
	AlreadySubmitted,
	SubmissionAlreadyExists,
	SubmissionNotFound,
	UserNotFound,
}

public readonly record struct TaskOperationResult(TaskOperationError Error)
{
	public bool IsSuccess => Error == TaskOperationError.None;

	public static TaskOperationResult Success()
		=> new(TaskOperationError.None);

	public static TaskOperationResult Fail(TaskOperationError error)
		=> new(error);
}

public readonly record struct TaskOperationResult<T>(T? Value, TaskOperationError Error)
{
	public bool IsSuccess => Error == TaskOperationError.None;

	public static TaskOperationResult<T> Success(T value)
		=> new(value, TaskOperationError.None);

	public static TaskOperationResult<T> Fail(TaskOperationError error)
		=> new(default, error);
}

public readonly record struct TaskSubmitOperationResult(bool IsCreated, TaskOperationError Error)
{
	public bool IsSuccess => Error == TaskOperationError.None;

	public static TaskSubmitOperationResult Created()
		=> new(true, TaskOperationError.None);

	public static TaskSubmitOperationResult Updated()
		=> new(false, TaskOperationError.None);

	public static TaskSubmitOperationResult Fail(TaskOperationError error)
		=> new(false, error);
}