namespace LdprActivistDemo.Application.Users;

public interface IUnconfirmedUserCleanupScheduler
{
	void Schedule(Guid userId);
}