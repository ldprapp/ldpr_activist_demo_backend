namespace LdprActivistDemo.Contracts.Users;

public sealed record RequestPasswordResetRequest(string PhoneNumber, string NewPassword);