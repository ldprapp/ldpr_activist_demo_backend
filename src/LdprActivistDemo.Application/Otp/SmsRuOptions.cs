namespace LdprActivistDemo.Application.Otp;

/// <summary>
/// Настройки интеграции с SMS.ru.
/// </summary>
public sealed class SmsRuOptions
{
	/// <summary>
	/// Имя конфигурационной секции.
	/// </summary>
	public const string SectionName = "SmsRu";

	public string ApiId { get; set; } = string.Empty;
	public bool IsTestMode { get; set; } = true;
	public string BaseUrl { get; set; } = "https://sms.ru";
}