namespace LdprActivistDemo.Api.Logging;

/// <summary>
/// Единый каталог operation/event-name значений для structured logging в API.
/// </summary>
public static class ApiLogOperations
{
	public static class Host
	{
		public const string ApplicationStarted = "host.application.started";
		public const string ApplicationStopping = "host.application.stopping";
		public const string TerminatedUnexpectedly = "host.application.terminated_unexpectedly";
	}

	public static class Startup
	{
		public const string ConfigureBootstrapLogger = "startup.bootstrap_logger.configure";
		public const string InitializeSequence = "startup.sequence.initialize";
		public const string ApplyMigrations = "startup.migrations.apply";
		public const string CleanupUnconfirmedUsers = "startup.users.cleanup_unconfirmed";
		public const string PipelineReady = "startup.pipeline.ready";
		public const string ResolveVersion = "startup.version.resolve";
	}

	public static class Http
	{
		public const string Request = "http.request";
		public const string UnhandledException = "http.unhandled_exception";
	}

	public static class Health
	{
		public const string GetHealth = "health.get";
		public const string GetVersion = "health.version.get";
	}

	public static class Images
	{
		public const string GetUserImage = "images.user.get";
		public const string DeleteUserImage = "images.user.delete";
		public const string GetSystemImage = "images.system.get";
		public const string UpsertSystemImage = "images.system.upsert";
	}

	public static class Geo
	{
		public const string GetRegions = "geo.regions.list";
		public const string GetSettlementsByRegion = "geo.settlements.list_by_region";
		public const string CreateRegion = "geo.region.create";
		public const string CreateSettlements = "geo.settlements.create";
		public const string UpdateRegion = "geo.region.update";
		public const string DeleteRegion = "geo.region.delete";
		public const string RestoreRegion = "geo.region.restore";
		public const string UpdateSettlement = "geo.settlement.update";
		public const string DeleteSettlement = "geo.settlement.delete";
		public const string RestoreSettlement = "geo.settlement.restore";
	}

	public static class Tasks
	{
		public const string Create = "tasks.create";
		public const string Update = "tasks.update";
		public const string Close = "tasks.close";
		public const string Open = "tasks.open";
		public const string Get = "tasks.get";
		public const string GetFeed = "tasks.feed.get";
		public const string CreateSubmission = "tasks.submission.create";
		public const string SubmitForReview = "tasks.submission.submit_for_review";
		public const string UpdateSubmission = "tasks.submission.update";
		public const string GetReviewerSubmissionFeed = "tasks.submission.feed.reviewer";
		public const string GetExecutorSubmissionFeed = "tasks.submission.feed.executor";
		public const string GetSubmission = "tasks.submission.get";
		public const string GetTaskUsers = "tasks.users.get";
		public const string ApproveSubmission = "tasks.submission.approve";
		public const string RejectSubmission = "tasks.submission.reject";
	}

	public static class UserPoints
	{
		public const string GetBalance = "user_points.balance.get";
		public const string GetTransactions = "user_points.transactions.get";
		public const string CreateTransaction = "user_points.transactions.create";
		public const string CancelTransaction = "user_points.transactions.cancel";
		public const string RestoreTransaction = "user_points.transactions.restore";
	}

	public static class Users
	{
		public const string Register = "users.register";
		public const string SendOtp = "users.otp.send";
		public const string RequestPasswordReset = "users.password_reset.request";
		public const string ConfirmPasswordReset = "users.password_reset.confirm";
		public const string ConfirmPhone = "users.phone.confirm";
		public const string Login = "users.login";
		public const string GetByPhone = "users.get_by_phone";
		public const string GetById = "users.get";
		public const string ChangePassword = "users.password.change";
		public const string Update = "users.update";
		public const string ChangePhone = "users.phone.change";
		public const string GetFeed = "users.feed.get";
		public const string GetRole = "users.role.get";
		public const string GrantCoordinatorRole = "users.role.coordinator.grant";
		public const string RevokeCoordinatorRole = "users.role.coordinator.revoke";
	}

	public static string BuildFallback(string? controller, string? action)
	{
		var controllerToken = NormalizeToken(controller, "unknown");
		var actionToken = NormalizeToken(action, "unknown");
		return $"api.{controllerToken}.{actionToken}";
	}

	private static string NormalizeToken(string? value, string fallback)
	{
		var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
		if(normalized.Length == 0)
		{
			return fallback;
		}

		var chars = normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '.').ToArray();
		var token = string.Join(".", new string(chars)
			.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

		return token.Length == 0 ? fallback : token;
	}
}