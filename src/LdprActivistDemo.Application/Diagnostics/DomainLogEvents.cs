namespace LdprActivistDemo.Application.Diagnostics;

public static class DomainLogEvents
{
	public static class Http
	{
		public const string Request = "http.middleware.request";
		public const string RequestStarted = Request;
		public const string RequestCompleted = Request;
		public const string RequestAborted = Request;
		public const string UnhandledException = "http.middleware.unhandled_exception";
	}

	public static class Api
	{
		public const string Action = "api.controller.action";
		public const string ActionStarted = Action;
		public const string ActionCompleted = Action;
		public const string Problem = "api.controller.problem";
	}

	public static class Startup
	{
		public const string SequenceInitialized = "startup.host.sequence.initialized";
		public const string ApplicationStarted = "startup.host.application.started";
		public const string ApplicationStopping = "startup.host.application.stopping";
		public const string MigrationsApply = "startup.host.migrations.apply";
		public const string CleanupUnconfirmedUsers = "startup.host.cleanup_unconfirmed_users";
		public const string PipelineReady = "startup.host.pipeline.ready";
	}

	public static class Auth
	{
		public const string ActorAuthenticate = "auth.service.actor.authenticate";
	}

	public static class Geo
	{
		public static class Service
		{
			public const string GetRegions = "geo.service.regions.list";
			public const string GetSettlementsByRegion = "geo.service.settlements.list_by_region";
			public const string CreateRegion = "geo.service.region.create";
			public const string CreateSettlements = "geo.service.settlements.create";
			public const string UpdateRegion = "geo.service.region.update";
			public const string UpdateSettlement = "geo.service.settlement.update";
			public const string DeleteRegion = "geo.service.region.delete";
			public const string DeleteSettlement = "geo.service.settlement.delete";
			public const string RestoreRegion = "geo.service.region.restore";
			public const string RestoreSettlement = "geo.service.settlement.restore";
		}

		public static class Repository
		{
			public const string GetRegions = "geo.repository.regions.list";
			public const string GetSettlementsByRegion = "geo.repository.settlements.list_by_region";
			public const string CreateRegion = "geo.repository.region.create";
			public const string CreateSettlements = "geo.repository.settlements.create";
			public const string UpdateRegion = "geo.repository.region.update";
			public const string UpdateSettlement = "geo.repository.settlement.update";
			public const string DeleteRegion = "geo.repository.region.delete";
			public const string DeleteSettlement = "geo.repository.settlement.delete";
			public const string RestoreRegion = "geo.repository.region.restore";
			public const string RestoreSettlement = "geo.repository.settlement.restore";
		}

		public const string GetRegions = Service.GetRegions;
		public const string GetSettlementsByRegion = Service.GetSettlementsByRegion;
		public const string CreateRegion = Service.CreateRegion;
		public const string CreateSettlements = Service.CreateSettlements;
		public const string UpdateRegion = Service.UpdateRegion;
		public const string UpdateSettlement = Service.UpdateSettlement;
		public const string DeleteRegion = Service.DeleteRegion;
		public const string DeleteSettlement = Service.DeleteSettlement;
		public const string RestoreRegion = Service.RestoreRegion;
		public const string RestoreSettlement = Service.RestoreSettlement;
	}

	public static class Image
	{
		public static class Service
		{
			public const string Get = "images.service.user.get";
			public const string GetSystemByName = "images.service.system.get";
			public const string Create = "images.service.user.create";
			public const string CreateMany = "images.service.user.create_many";
			public const string Delete = "images.service.user.delete";
			public const string UpsertSystem = "images.service.system.upsert";
		}

		public static class Repository
		{
			public const string Get = "images.repository.user.get";
			public const string GetSystemByName = "images.repository.system.get";
			public const string GetOwnerUserId = "images.repository.user.owner.get";
			public const string IsUsedBySystemImage = "images.repository.system.usage.check";
			public const string Create = "images.repository.user.create";
			public const string CreateMany = "images.repository.user.create_many";
			public const string Delete = "images.repository.user.delete";
			public const string UpsertSystem = "images.repository.system.upsert";
		}

		public const string Get = Service.Get;
		public const string GetSystemByName = Service.GetSystemByName;
		public const string Create = Service.Create;
		public const string CreateMany = Service.CreateMany;
		public const string Delete = Service.Delete;
		public const string UpsertSystem = Service.UpsertSystem;
	}

	public static class Otp
	{
		public static class Service
		{
			public const string Issue = "otp.service.issue";
			public const string Verify = "otp.service.verify";
		}

		public static class Sender
		{
			public const string Send = "otp.sender.send";
		}

		public static class Store
		{
			public const string Set = "otp.store.set";
			public const string Get = "otp.store.get";
			public const string Remove = "otp.store.remove";
		}

		public const string Issue = Service.Issue;
		public const string Verify = Service.Verify;
		public const string Send = Sender.Send;
	}

	public static class PasswordReset
	{
		public static class Service
		{
			public const string Issue = "password_reset.service.issue";
			public const string Confirm = "password_reset.service.confirm";
		}

		public static class Store
		{
			public const string Set = "password_reset.store.set";
			public const string Get = "password_reset.store.get";
			public const string Remove = "password_reset.store.remove";
		}

		public static class Repository
		{
			public const string UpdatePasswordHash = "password_reset.repository.password_hash.set";
		}

		public const string Issue = Service.Issue;
		public const string Confirm = Service.Confirm;
		public const string UpdatePasswordHash = Repository.UpdatePasswordHash;
	}

	public static class Task
	{
		public static class Service
		{
			public const string ValidateActor = "tasks.service.actor.validate";
			public const string Create = "tasks.service.create";
			public const string Update = "tasks.service.update";
			public const string Open = "tasks.service.open";
			public const string Close = "tasks.service.close";
			public const string GetCoordinator = "tasks.service.coordinator.get";
			public const string GetPublic = "tasks.service.public.get";
			public const string GetByRegionAndSettlement = "tasks.service.list.by_region_and_settlement";
			public const string GetByRegion = "tasks.service.list.by_region";
			public const string GetBySettlement = "tasks.service.list.by_settlement";
			public const string GetByCoordinator = "tasks.service.list.by_coordinator";
			public const string GetAvailableForUser = "tasks.service.list.available_for_user";
			public const string GetByUserSubmitted = "tasks.service.list.by_user_submitted";
			public const string GetByUserApproved = "tasks.service.list.by_user_approved";
			public const string GetAllIds = "tasks.service.feed.all_ids.get";
		}

		public static class Repository
		{
			public const string Create = "tasks.repository.create";
			public const string Update = "tasks.repository.update";
			public const string Open = "tasks.repository.open";
			public const string Close = "tasks.repository.close";
			public const string GetCoordinator = "tasks.repository.coordinator.get";
			public const string GetPublic = "tasks.repository.public.get";
			public const string GetByRegionAndSettlement = "tasks.repository.list.by_region_and_settlement";
			public const string GetByRegion = "tasks.repository.list.by_region";
			public const string GetBySettlement = "tasks.repository.list.by_settlement";
			public const string GetByCoordinator = "tasks.repository.list.by_coordinator";
			public const string GetAvailableForUser = "tasks.repository.list.available_for_user";
			public const string GetByUserSubmitted = "tasks.repository.list.by_user_submitted";
			public const string GetByUserApproved = "tasks.repository.list.by_user_approved";
			public const string GetAllIds = "tasks.repository.feed.all_ids.get";
		}

		public const string ValidateActor = Service.ValidateActor;
		public const string Create = Service.Create;
		public const string Update = Service.Update;
		public const string Open = Service.Open;
		public const string Close = Service.Close;
		public const string GetCoordinator = Service.GetCoordinator;
		public const string GetPublic = Service.GetPublic;
		public const string GetByRegionAndSettlement = Service.GetByRegionAndSettlement;
		public const string GetByRegion = Service.GetByRegion;
		public const string GetBySettlement = Service.GetBySettlement;
		public const string GetByCoordinator = Service.GetByCoordinator;
		public const string GetAvailableForUser = Service.GetAvailableForUser;
		public const string GetByUserSubmitted = Service.GetByUserSubmitted;
		public const string GetByUserApproved = Service.GetByUserApproved;
		public const string GetAllIds = Service.GetAllIds;
	}

	public static class TaskSubmission
	{
		public static class Service
		{
			public const string Submit = "tasks.submission.service.create";
			public const string SubmitForReview = "tasks.submission.service.submit_for_review";
			public const string Update = "tasks.submission.service.update";
			public const string Delete = "tasks.submission.service.delete";
			public const string Approve = "tasks.submission.service.approve";
			public const string Reject = "tasks.submission.service.reject";
			public const string GetSubmittedUsers = "tasks.submission.service.users_submitted.get";
			public const string GetApprovedUsers = "tasks.submission.service.users_approved.get";
			public const string GetSubmittedUser = "tasks.submission.service.user_submitted.get";
			public const string GetReviewerFeed = "tasks.submission.service.feed.reviewer";
			public const string GetExecutorFeed = "tasks.submission.service.feed.executor";
			public const string GetTaskIdsByUserDecisionStatus = "tasks.submission.service.task_ids_by_user_decision_status.get";
			public const string GetById = "tasks.submission.service.get";
			public const string GetTaskUsers = "tasks.submission.service.users.get";
		}

		public static class Repository
		{
			public const string Submit = "tasks.submission.repository.create";
			public const string SubmitForReview = "tasks.submission.repository.submit_for_review";
			public const string Update = "tasks.submission.repository.update";
			public const string Delete = "tasks.submission.repository.delete";
			public const string Approve = "tasks.submission.repository.approve";
			public const string Reject = "tasks.submission.repository.reject";
			public const string GetSubmittedUsers = "tasks.submission.repository.users_submitted.get";
			public const string GetApprovedUsers = "tasks.submission.repository.users_approved.get";
			public const string GetSubmittedUser = "tasks.submission.repository.user_submitted.get";
			public const string GetReviewerFeed = "tasks.submission.repository.feed.reviewer";
			public const string GetExecutorFeed = "tasks.submission.repository.feed.executor";
			public const string GetTaskIdsByUserDecisionStatus = "tasks.submission.repository.task_ids_by_user_decision_status.get";
			public const string GetById = "tasks.submission.repository.get";
			public const string GetTaskUsers = "tasks.submission.repository.users.get";
		}

		public const string Submit = Service.Submit;
		public const string SubmitForReview = Service.SubmitForReview;
		public const string Update = Service.Update;
		public const string Delete = Service.Delete;
		public const string Approve = Service.Approve;
		public const string Reject = Service.Reject;
		public const string GetSubmittedUsers = Service.GetSubmittedUsers;
		public const string GetApprovedUsers = Service.GetApprovedUsers;
		public const string GetSubmittedUser = Service.GetSubmittedUser;
		public const string GetReviewerFeed = Service.GetReviewerFeed;
		public const string GetExecutorFeed = Service.GetExecutorFeed;
		public const string GetTaskIdsByUserDecisionStatus = Service.GetTaskIdsByUserDecisionStatus;
		public const string GetById = Service.GetById;
		public const string GetTaskUsers = Service.GetTaskUsers;
	}

	public static class User
	{
		public static class Service
		{
			public const string Register = "users.service.register";
			public const string ConfirmPhone = "users.service.phone.confirm";
			public const string Login = "users.service.login";
			public const string GetByPhone = "users.service.get_by_phone";
			public const string GetById = "users.service.get";
			public const string UpdateProfile = "users.service.profile.update";
			public const string ChangePhone = "users.service.phone.change";
			public const string ChangePassword = "users.service.password.change";
			public const string SetAvatar = "users.service.avatar.set";
			public const string GetUsersByRegion = "users.service.list.by_region";
			public const string GetUsersBySettlement = "users.service.list.by_settlement";
			public const string GetUsersByRegionAndSettlement = "users.service.list.by_region_and_settlement";
			public const string GetUsers = "users.service.list.get";
			public const string GetRole = "users.service.role.get";
			public const string ChangeCoordinatorRole = "users.service.role.coordinator.change";
		}

		public static class Background
		{
			public const string CleanupSchedule = "users.background.cleanup.schedule";
			public const string CleanupRun = "users.background.cleanup.run";
		}

		public static class Repository
		{
			public const string GetInternalById = "users.repository.internal.get_by_id";
			public const string GetInternalByPhone = "users.repository.internal.get_by_phone";
			public const string GetPublicById = "users.repository.public.get_by_id";
			public const string GetPublicByPhone = "users.repository.public.get_by_phone";
			public const string ExistsConfirmedByPhone = "users.repository.phone.confirmed.exists";
			public const string DeleteUnconfirmedByPhone = "users.repository.unconfirmed.delete_by_phone";
			public const string DeleteUnconfirmedById = "users.repository.unconfirmed.delete_by_id";
			public const string DeleteAllUnconfirmed = "users.repository.unconfirmed.delete_all";
			public const string Create = "users.repository.create";
			public const string ValidatePasswordByPhone = "users.repository.password.validate_by_phone";
			public const string ValidatePasswordById = "users.repository.password.validate_by_id";
			public const string SetPhoneConfirmed = "users.repository.phone.confirmed.set";
			public const string SetPassword = "users.repository.password.set";
			public const string Update = "users.repository.update";
			public const string SetAvatar = "users.repository.avatar.set";
			public const string ChangePhone = "users.repository.phone.change";
			public const string GetByFilters = "users.repository.list.get_by_filters";
			public const string SetRole = "users.repository.role.set";
			public const string GetRole = "users.repository.role.get";
		}

		public const string Register = Service.Register;
		public const string ConfirmPhone = Service.ConfirmPhone;
		public const string Login = Service.Login;
		public const string GetByPhone = Service.GetByPhone;
		public const string GetById = Service.GetById;
		public const string UpdateProfile = Service.UpdateProfile;
		public const string ChangePhone = Service.ChangePhone;
		public const string ChangePassword = Service.ChangePassword;
		public const string SetAvatar = Service.SetAvatar;
		public const string GetUsersByRegion = Service.GetUsersByRegion;
		public const string GetUsersBySettlement = Service.GetUsersBySettlement;
		public const string GetUsersByRegionAndSettlement = Service.GetUsersByRegionAndSettlement;
		public const string GetUsers = Service.GetUsers;
		public const string GetRole = Service.GetRole;
		public const string ChangeCoordinatorRole = Service.ChangeCoordinatorRole;
		public const string CleanupSchedule = Background.CleanupSchedule;
		public const string CleanupRun = Background.CleanupRun;
	}

	public static class UserPoints
	{
		public static class Service
		{
			public const string GetBalance = "user_points.service.balance.get";
			public const string GetTransactions = "user_points.service.transactions.get";
			public const string CreateTransaction = "user_points.service.transactions.create";
			public const string CancelTransaction = "user_points.service.transactions.cancel";
			public const string RestoreTransaction = "user_points.service.transactions.restore";
		}

		public static class Repository
		{
			public const string GetBalance = "user_points.repository.balance.get";
			public const string GetTransactions = "user_points.repository.transactions.get";
			public const string CreateTransaction = "user_points.repository.transactions.create";
			public const string CancelTransaction = "user_points.repository.transactions.cancel";
			public const string RestoreTransaction = "user_points.repository.transactions.restore";
		}

		public const string GetBalance = Service.GetBalance;
		public const string GetTransactions = Service.GetTransactions;
		public const string CreateTransaction = Service.CreateTransaction;
		public const string CancelTransaction = Service.CancelTransaction;
		public const string RestoreTransaction = Service.RestoreTransaction;
	}

	public static class UserRatings
	{
		public static class Service
		{
			public const string GetFeed = "user_ratings.service.feed.get";
			public const string GetUserRanks = "user_ratings.service.user.get";
			public const string GetRefreshSchedule = "user_ratings.service.refresh_schedule.get";
			public const string SetRefreshSchedule = "user_ratings.service.refresh_schedule.set";
			public const string RunRefreshNow = "user_ratings.service.refresh.run_now";
		}

		public static class Repository
		{
			public const string GetFeed = "user_ratings.repository.feed.get";
			public const string GetUserRanks = "user_ratings.repository.user.get";
		}

		public static class Background
		{
			public const string RefreshSchedule = "user_ratings.background.refresh.schedule";
			public const string RefreshRun = "user_ratings.background.refresh.run";
		}

		public const string GetFeed = Service.GetFeed;
		public const string GetUserRanks = Service.GetUserRanks;
		public const string GetRefreshSchedule = Service.GetRefreshSchedule;
		public const string SetRefreshSchedule = Service.SetRefreshSchedule;
		public const string RunRefreshNow = Service.RunRefreshNow;
		public const string RefreshSchedule = Background.RefreshSchedule;
		public const string RefreshRun = Background.RefreshRun;
	}
}