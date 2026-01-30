namespace LdprActivistDemo.Persistence;

public sealed class ImageEntity
{
	public Guid Id { get; set; }

	public string ContentType { get; set; } = "application/octet-stream";

	public byte[] Data { get; set; } = Array.Empty<byte>();
}