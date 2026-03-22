namespace LdprActivistDemo.Application.UserPoints.Models;

public enum UserPointsError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
	UserNotFound = 4,
	InsufficientBalance = 5,
	TransactionNotFound = 6,
}