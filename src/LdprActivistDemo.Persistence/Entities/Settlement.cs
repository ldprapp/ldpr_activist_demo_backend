namespace LdprActivistDemo.Persistence;

public sealed class Settlement
{
	public int Id { get; set; }

	public int RegionId { get; set; }

	public Region Region { get; set; } = null!;

	public string Name { get; set; } = string.Empty;

	public bool IsDeleted { get; set; }
}