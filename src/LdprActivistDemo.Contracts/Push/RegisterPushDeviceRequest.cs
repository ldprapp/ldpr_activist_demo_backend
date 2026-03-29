using System.ComponentModel.DataAnnotations;

namespace LdprActivistDemo.Contracts.Push;

public sealed record RegisterPushDeviceRequest(
	[Required] string Token,
	[Required] string Platform);