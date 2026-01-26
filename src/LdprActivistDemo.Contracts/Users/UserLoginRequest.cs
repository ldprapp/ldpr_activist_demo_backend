namespace LdprActivistDemo.Contracts.Users;

public sealed record UserLoginRequest(string PhoneNumber, string Password);