using System.Globalization;

namespace LdprActivistDemo.Api.Time;

/// <summary>
/// Централизованно применяет локаль времени ко всему приложению.
/// </summary>
public static class ApplicationTimeCultureConfigurator
{
	/// <summary>
	/// Применяет локаль как текущую и как локаль по умолчанию для новых потоков приложения.
	/// </summary>
	/// <param name="locale">Строковый идентификатор локали.</param>
	/// <returns>Итоговая применённая культура.</returns>
	public static CultureInfo ApplyDefaultCulture(string? locale)
	{
		var culture = ResolveCulture(locale);

		CultureInfo.CurrentCulture = culture;
		CultureInfo.CurrentUICulture = culture;
		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.DefaultThreadCurrentUICulture = culture;

		return culture;
	}

	/// <summary>
	/// Разрешает строковый идентификатор локали в экземпляр <see cref="CultureInfo"/>.
	/// </summary>
	/// <param name="locale">Строковый идентификатор локали.</param>
	/// <returns>Нормализованная культура.</returns>
	public static CultureInfo ResolveCulture(string? locale)
	{
		var normalized = string.IsNullOrWhiteSpace(locale) ? "ru-RU" : locale.Trim();
		return CultureInfo.GetCultureInfo(normalized);
	}
}