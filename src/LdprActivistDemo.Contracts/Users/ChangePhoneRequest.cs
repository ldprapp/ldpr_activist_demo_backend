namespace LdprActivistDemo.Contracts.Users;

public sealed record ChangePhoneRequest(
	string NewPhoneNumber,
	string OtpCode);