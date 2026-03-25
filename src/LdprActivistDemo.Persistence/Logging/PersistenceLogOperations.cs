namespace LdprActivistDemo.Persistence.Logging;

/// <summary>
/// Единый каталог operation.name для persistence-слоя.
/// </summary>
public static class PersistenceLogOperations
{
	public static class OtpStore
	{
		public const string Set = "otp.store.set";
		public const string Get = "otp.store.get";
		public const string Remove = "otp.store.remove";
	}

	public static class PasswordReset
	{
		public const string SetPasswordHash = "password_reset.password_hash.set";

		public static class Store
		{
			public const string Set = "password_reset.store.set";
			public const string Get = "password_reset.store.get";
			public const string Remove = "password_reset.store.remove";
		}
	}

	public static class Geo
	{
		public const string GetRegions = "geo.regions.list";
		public const string GetSettlementsByRegion = "geo.settlements.list_by_region";
		public const string ExistsRegionByName = "geo.region.exists_by_name";
		public const string GetRegionIdByName = "geo.region.id.get_by_name";
		public const string GetSettlementIdByRegionAndName = "geo.settlement.id.get_by_region_and_name";
		public const string CreateRegion = "geo.region.create";
		public const string CreateSettlements = "geo.settlements.create";
		public const string UpdateRegion = "geo.region.update";
		public const string UpdateSettlement = "geo.settlement.update";
		public const string DeleteRegion = "geo.region.delete";
		public const string DeleteSettlement = "geo.settlement.delete";
		public const string RestoreRegion = "geo.region.restore";
		public const string RestoreSettlement = "geo.settlement.restore";
	}

	public static class Images
	{
		public const string Get = "images.user.get";
		public const string GetSystemByName = "images.system.get";
		public const string Create = "images.user.create";
		public const string CreateMany = "images.user.create_many";
		public const string Delete = "images.user.delete";
		public const string UpsertSystem = "images.system.upsert";
		public const string GetOwnerUserId = "images.user.owner.get";
		public const string IsUsedBySystemImage = "images.system.usage.check";
	}

	public static class Tasks
	{
		public const string Create = "tasks.create";
		public const string Update = "tasks.update";
		public const string Open = "tasks.open";
		public const string Close = "tasks.close";
		public const string GetCoordinator = "tasks.coordinator.get";
		public const string GetPublic = "tasks.public.get";
		public const string GetByRegionAndSettlement = "tasks.list.by_region_and_settlement";
		public const string GetByRegion = "tasks.list.by_region";
		public const string GetBySettlement = "tasks.list.by_settlement";
		public const string GetByCoordinator = "tasks.list.by_coordinator";
		public const string GetAvailableForUser = "tasks.list.available_for_user";
		public const string GetByUserSubmitted = "tasks.list.by_user_submitted";
		public const string GetByUserApproved = "tasks.list.by_user_approved";
		public const string GetAllIds = "tasks.feed.all_ids.get";
	}

	public static class TaskSubmissions
	{
		public const string Submit = "tasks.submission.create";
		public const string SubmitForReview = "tasks.submission.submit_for_review";
		public const string Update = "tasks.submission.update";
		public const string Delete = "tasks.submission.delete";
		public const string Approve = "tasks.submission.approve";
		public const string Reject = "tasks.submission.reject";
		public const string GetSubmittedUsers = "tasks.submission.users_submitted.get";
		public const string GetApprovedUsers = "tasks.submission.users_approved.get";
		public const string GetSubmittedUser = "tasks.submission.user_submitted.get";
		public const string GetReviewerFeed = "tasks.submission.feed.reviewer";
		public const string GetExecutorFeed = "tasks.submission.feed.executor";
		public const string GetTaskIdsByUserDecisionStatus = "tasks.submission.task_ids_by_user_decision_status.get";
		public const string GetTaskIdsWithAnySubmissionByUser = "tasks.submission.task_ids_with_any_submission_by_user.get";
		public const string GetById = "tasks.submission.get";
		public const string GetTaskUsers = "tasks.users.get";
	}

	public static class Users
	{
		public const string GetInternalById = "users.internal.get_by_id";
		public const string GetInternalByPhone = "users.internal.get_by_phone";
		public const string GetPublicById = "users.public.get_by_id";
		public const string GetPublicByPhone = "users.public.get_by_phone";
		public const string ExistsConfirmedByPhone = "users.phone.confirmed.exists";
		public const string DeleteUnconfirmedByPhone = "users.unconfirmed.delete_by_phone";
		public const string DeleteUnconfirmedById = "users.unconfirmed.delete_by_id";
		public const string DeleteAllUnconfirmed = "users.unconfirmed.delete_all";
		public const string ValidatePasswordByPhone = "users.password.validate_by_phone";
		public const string ValidatePasswordById = "users.password.validate_by_id";
		public const string SetPhoneConfirmed = "users.phone.confirmed.set";
		public const string SetPassword = "users.password.set";
		public const string SetAvatar = "users.avatar.set";
		public const string GetByFilters = "users.list.get_by_filters";
		public const string SetRole = "users.role.set";
		public const string GetRole = "users.role.get";
	}

	public static class UserPoints
	{
		public const string GetBalance = "user_points.balance.get";
		public const string GetTransactions = "user_points.transactions.get";
		public const string CreateTransaction = "user_points.transactions.create";
		public const string CancelTransaction = "user_points.transactions.cancel";
		public const string RestoreTransaction = "user_points.transactions.restore";
	}

	public static class UserRatings
	{
		public const string GetFeed = "user_ratings.feed.get";
		public const string GetUserRanks = "user_ratings.user.get";
	}
}