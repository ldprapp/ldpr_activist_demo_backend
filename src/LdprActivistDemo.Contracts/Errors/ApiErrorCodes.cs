namespace LdprActivistDemo.Contracts.Errors;

/// <summary>
/// Стабильные коды ошибок API для <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>.
/// </summary>
public static class ApiErrorCodes
{
	public const string ValidationFailed = "validation_failed";
	public const string InvalidCredentials = "invalid_credentials";
	public const string PhoneNotConfirmed = "phone_not_confirmed";
	public const string OtpInvalid = "otp_invalid";
	public const string PhoneAlreadyConfirmed = "phone_already_confirmed";
	public const string OtpSendFailed = "otp_send_failed";
	public const string PasswordResetExpired = "password_reset_expired";

	public const string UserNotFound = "user_not_found";
	public const string PhoneAlreadyExists = "phone_already_exists";
	public const string ReferralCodeNotFound = "referral_code_not_found";
	public const string GenderInvalid = "gender_invalid";
	public const string SettlementRegionMismatch = "settlement_region_mismatch";
	public const string UserRoleChangeNotAllowed = "user_role_change_not_allowed";
	public const string PhoneChangeNotAllowed = "phone_change_not_allowed";

	public const string GeoRegionNotFound = "geo_region_not_found";
	public const string GeoSettlementNotFound = "geo_settlement_not_found";
	public const string GeoDuplicate = "geo_duplicate";
	public const string GeoInvalidName = "geo_invalid_name";
	public const string GeoUnauthorized = "geo_unauthorized";
	public const string GeoInUse = "geo_in_use";
	public const string GeoHasActiveSettlements = "geo_has_active_settlements";
	public const string GeoParentRegionDeleted = "geo_parent_region_deleted";

	public const string Forbidden = "forbidden";
	public const string TaskNotFound = "task_not_found";
	public const string TaskClosed = "task_closed";
	public const string TaskAutoVerificationNotSupported = "task_auto_verification_not_supported";
	public const string TaskAlreadySubmitted = "task_already_submitted";
	public const string TaskSubmissionExists = "task_submission_exists";
	public const string TaskSubmissionNotFound = "task_submission_not_found";
	public const string TaskAccessDenied = "task_access_denied";

	public const string ImageNotFound = "image_not_found";
	public const string ImageInUse = "image_in_use";
	public const string SystemImageNotFound = "system_image_not_found";

	public const string UserPointsInsufficientBalance = "user_points_insufficient_balance";
	public const string UserPointsTransactionNotFound = "user_points_transaction_not_found";

	public const string InternalError = "internal_error";
}