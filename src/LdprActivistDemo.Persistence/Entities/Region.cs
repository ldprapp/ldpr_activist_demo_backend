namespace LdprActivistDemo.Persistence;

public sealed class Region
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public List<City> Cities { get; set; } = new();
}