namespace LdprActivistDemo.Contracts.Geo;

public sealed class CreateCitiesRequest
{
	public List<string> Names { get; set; } = new();
}
