using LdprActivistDemo.Contracts.Users;

namespace LdprActivistDemo.Contracts.Tasks;

/// <remarks>
/// User здесь оставлен как object, потому что конкретный контракт пользователя в текущем сообщении не был предоставлен.
/// </remarks>
public sealed record SubmissionUserViewDto(
	UserDto User,
	SubmissionDto Submission);