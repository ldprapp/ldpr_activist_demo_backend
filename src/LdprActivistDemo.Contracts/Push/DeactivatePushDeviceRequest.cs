using System.ComponentModel.DataAnnotations;

namespace LdprActivistDemo.Contracts.Push;

public sealed record DeactivatePushDeviceRequest(
	[Required] string Token);