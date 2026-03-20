namespace LdprActivistDemo.Api.Time;

/// <summary>
/// Общие настройки времени и локали для API-приложения.
/// </summary>
public sealed class ApplicationTimeOptions
{
	/// <summary>
	/// Имя конфигурационной секции.
	/// </summary>
	public const string SectionName = "ApplicationTime";

	/// <summary>
	/// Локаль, которая применяется как базовая культура приложения для операций форматирования времени и дат.
	/// </summary>
	/// <value>
	/// Строковый идентификатор culture-info, например <c>ru-RU</c>.
	/// </value>
	public string Locale { get; set; } = "ru-RU";
}