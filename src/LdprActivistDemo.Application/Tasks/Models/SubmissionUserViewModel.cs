using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record SubmissionUserViewModel(UserPublicModel User, TaskSubmissionModel Submission);