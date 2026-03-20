namespace LdprActivistDemo.Application.Users.Models;

public static class UserRoles
{
	public const string Activist = "activist";
	public const string Coordinator = "coordinator";
	public const string Admin = "admin";
	public const string Banned = "banned";
}

public static class UserRoleRules
{
	public static bool TryNormalizeRequiredRole(
		string? raw,
		out string normalized,
		out string? error)
	{
		error = null;
		normalized = string.Empty;

		if(string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			error =
				$"Role must be '{UserRoles.Activist}', '{UserRoles.Coordinator}', '{UserRoles.Admin}' or '{UserRoles.Banned}'.";
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();
		if(string.Equals(token, UserRoles.Activist, StringComparison.Ordinal))
		{
			normalized = UserRoles.Activist;
			return true;
		}

		if(string.Equals(token, UserRoles.Coordinator, StringComparison.Ordinal))
		{
			normalized = UserRoles.Coordinator;
			return true;
		}

		if(string.Equals(token, UserRoles.Admin, StringComparison.Ordinal))
		{
			normalized = UserRoles.Admin;
			return true;
		}

		if(string.Equals(token, UserRoles.Banned, StringComparison.Ordinal))
		{
			normalized = UserRoles.Banned;
			return true;
		}

		error =
			$"Role must be '{UserRoles.Activist}', '{UserRoles.Coordinator}', '{UserRoles.Admin}' or '{UserRoles.Banned}'.";
		return false;
	}

	public static bool TryNormalizeOptionalRole(
		string? raw,
		out string? normalized,
		out string? error)
	{
		error = null;
		normalized = null;

		if(string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if(!TryNormalizeRequiredRole(raw, out var required, out error))
		{
			return false;
		}

		normalized = required;
		return true;
	}

	public static bool HasCoordinatorAccess(string? role)
		=> string.Equals(role, UserRoles.Coordinator, StringComparison.Ordinal) || IsAdmin(role);

	public static bool IsAdmin(string? role)
		=> string.Equals(role, UserRoles.Admin, StringComparison.Ordinal);

	public static bool IsBanned(string? role)
		=> string.Equals(role, UserRoles.Banned, StringComparison.Ordinal);
}