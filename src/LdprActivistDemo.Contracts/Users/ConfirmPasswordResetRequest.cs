namespace LdprActivistDemo.Contracts.Users;

public sealed record ConfirmPasswordResetRequest(string PhoneNumber, string OtpCode);