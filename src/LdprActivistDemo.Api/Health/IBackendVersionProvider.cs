namespace LdprActivistDemo.Api.Health;

/// <summary>Провайдер версии бэкенда.</summary>
public interface IBackendVersionProvider
{
	/// <summary>Версия бэкенда (строка из файла <c>version</c>).</summary>
	string Version { get; }
}