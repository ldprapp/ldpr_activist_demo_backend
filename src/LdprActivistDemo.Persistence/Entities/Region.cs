namespace LdprActivistDemo.Persistence;

public sealed class Region
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public bool IsDeleted { get; set; }

	public List<Settlement> Settlements { get; set; } = new();
}