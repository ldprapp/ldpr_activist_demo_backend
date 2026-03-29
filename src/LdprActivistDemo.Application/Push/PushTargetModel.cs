namespace LdprActivistDemo.Application.Push;

/// <summary>
/// Описывает активную цель для отправки push-уведомления.
/// </summary>
public sealed record PushTargetModel(
	Guid UserId,
	string Token,
	string Platform);