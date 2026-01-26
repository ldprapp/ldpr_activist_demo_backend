namespace LdprActivistDemo.Contracts.Users;

public sealed record ConfirmPhoneRequest(string PhoneNumber, string OtpCode);