namespace LdprActivistDemo.Application.Logging;

/// <summary>
/// Единый каталог operation.name для application-слоя.
/// </summary>
public static class ApplicationLogOperations
{
	public static class Auth
	{
		public const string AuthenticateActor = "auth.actor.authenticate";
	}

	public static class Geo
	{
		public const string GetRegions = "geo.regions.list";
		public const string GetSettlementsByRegion = "geo.settlements.list_by_region";
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
	}

	public static class Referrals
	{
		public const string GetContent = "referrals.content.get";
		public const string GetInvitedUsers = "referrals.invited_users.get";
		public const string GetSettings = "referrals.settings.get";
		public const string UpdateSettings = "referrals.settings.update";
		public const string SetInviteTextTemplate = "referrals.template.set";
		public const string SetInviterRewardPoints = "referrals.inviter_reward_points.set";
		public const string SetInvitedUserRewardPoints = "referrals.invited_user_reward_points.set";
	}

	public static class Otp
	{
		public const string Issue = "otp.issue";
		public const string Verify = "otp.verify";
		public const string SendMock = "otp.sender.mock.send";
		public const string SendSmsRu = "otp.sender.sms_ru.send";
	}

	public static class PasswordReset
	{
		public const string Issue = "password_reset.issue";
		public const string Confirm = "password_reset.confirm";
	}

	public static class Tasks
	{
		public const string ValidateActor = "tasks.actor.validate";
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
		public const string Submit = "tasks.submission.create";
		public const string SubmitForReview = "tasks.submission.submit_for_review";
		public const string UpdateSubmission = "tasks.submission.update";
		public const string DeleteSubmission = "tasks.submission.delete";
		public const string GetSubmittedUsers = "tasks.submission.users_submitted.get";
		public const string GetApprovedUsers = "tasks.submission.users_approved.get";
		public const string GetSubmittedUser = "tasks.submission.user_submitted.get";
		public const string ApproveSubmission = "tasks.submission.approve";
		public const string RejectSubmission = "tasks.submission.reject";
		public const string GetByUserSubmitted = "tasks.list.by_user_submitted";
		public const string GetByUserApproved = "tasks.list.by_user_approved";
		public const string GetSubmissionReviewerFeed = "tasks.submission.feed.reviewer";
		public const string GetSubmissionExecutorFeed = "tasks.submission.feed.executor";
		public const string GetTaskIdsByUserSubmissionStatus = "tasks.submission.task_ids_by_user_status.get";
		public const string GetTaskIdsWithAnySubmissionByUser = "tasks.submission.task_ids_with_any_submission_by_user.get";
		public const string GetSubmissionById = "tasks.submission.get";
		public const string GetTaskUsers = "tasks.users.get";
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
		public const string GetRefreshSchedule = "user_ratings.refresh_schedule.get";
		public const string SetRefreshSchedule = "user_ratings.refresh_schedule.set";
		public const string RunRefreshNow = "user_ratings.refresh.run_now";
		public const string RefreshSchedule = "user_ratings.refresh.schedule";
		public const string RefreshRun = "user_ratings.refresh.run";
	}

	public static class Users
	{
		public const string Register = "users.register";
		public const string ConfirmPhone = "users.phone.confirm";
		public const string Login = "users.login";
		public const string GetByPhone = "users.get_by_phone";
		public const string GetById = "users.get";
		public const string GetReferralCode = "users.referral_code.get";
		public const string ChangePassword = "users.password.change";
		public const string SetAvatar = "users.avatar.set";
		public const string UpdateProfile = "users.profile.update";
		public const string ChangePhone = "users.phone.change";
		public const string GetUsersByRegion = "users.list.by_region";
		public const string GetUsersBySettlement = "users.list.by_settlement";
		public const string GetUsersByRegionAndSettlement = "users.list.by_region_and_settlement";
		public const string GetUsers = "users.list.get";
		public const string GetRole = "users.role.get";
		public const string ChangeCoordinatorRole = "users.role.coordinator.change";
		public const string CleanupSchedule = "users.cleanup.schedule";
		public const string CleanupRun = "users.cleanup.run";
	}
}