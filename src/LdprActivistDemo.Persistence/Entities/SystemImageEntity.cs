namespace LdprActivistDemo.Persistence;

public sealed class SystemImageEntity
{
	public Guid Id { get; set; }

	public Guid ImageId { get; set; }

	public ImageEntity Image { get; set; } = null!;

	public string Name { get; set; } = string.Empty;
}