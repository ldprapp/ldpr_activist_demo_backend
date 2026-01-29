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
	public const string GenderInvalid = "gender_invalid";
	public const string CityRegionMismatch = "city_region_mismatch";
	public const string PhoneChangeNotAllowed = "phone_change_not_allowed";

	public const string GeoRegionNotFound = "geo_region_not_found";
	public const string GeoDuplicate = "geo_duplicate";
	public const string GeoInvalidName = "geo_invalid_name";
	public const string GeoUnauthorized = "geo_unauthorized";

	public const string Forbidden = "forbidden";
	public const string TaskNotFound = "task_not_found";
	public const string TaskClosed = "task_closed";
	public const string TaskAlreadySubmitted = "task_already_submitted";
	public const string TaskSubmissionExists = "task_submission_exists";
	public const string TaskSubmissionNotFound = "task_submission_not_found";
	public const string TaskAccessDenied = "task_access_denied";

	public const string InternalError = "internal_error";
}