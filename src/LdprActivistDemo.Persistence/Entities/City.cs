namespace LdprActivistDemo.Persistence;

public sealed class City
{
	public int Id { get; set; }

	public int RegionId { get; set; }

	public Region Region { get; set; } = null!;

	public string Name { get; set; } = string.Empty;
}