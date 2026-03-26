using LdprActivistDemo.Application.Referrals;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence;

/// <summary>
/// Репозиторий singleton-настроек реферальной системы.
/// </summary>
public sealed class ReferralSettingsRepository :
	IReferralRewardSettingsRepository,
	IReferralSettingsRepository
{
	private const int SingletonId = 1;
	private const string InviterRewardPointsPropertyName = "InviterRewardPoints";
	private const string InvitedUserRewardPointsPropertyName = "InvitedUserRewardPoints";

	private readonly AppDbContext _db;

	public ReferralSettingsRepository(AppDbContext db)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
	}

	public async Task<string> GetInviteTextTemplateAsync(CancellationToken cancellationToken)
	{
		var inviteTextTemplate = await _db.ReferralSettings
			.AsNoTracking()
			.Where(x => x.Id == SingletonId)
			.Select(x => x.InviteTextTemplate)
			.FirstOrDefaultAsync(cancellationToken);

		return string.IsNullOrWhiteSpace(inviteTextTemplate)
			? ReferralSettingsDefaults.InviteTextTemplate
			: inviteTextTemplate;
	}

	public async Task<int> GetInviterRewardPointsAsync(CancellationToken cancellationToken)
	{
		var inviterRewardPoints = await _db.ReferralSettings
			.AsNoTracking()
			.Where(x => x.Id == SingletonId)
			.Select(x => (int?)EF.Property<int>(x, InviterRewardPointsPropertyName))
			.FirstOrDefaultAsync(cancellationToken);

		return inviterRewardPoints ?? ReferralSettingsDefaults.InviterRewardPoints;
	}

	public async Task<int> GetInvitedUserRewardPointsAsync(CancellationToken cancellationToken)
	{
		var invitedUserRewardPoints = await _db.ReferralSettings
			.AsNoTracking()
			.Where(x => x.Id == SingletonId)
			.Select(x => (int?)EF.Property<int>(x, InvitedUserRewardPointsPropertyName))
			.FirstOrDefaultAsync(cancellationToken);

		return invitedUserRewardPoints ?? ReferralSettingsDefaults.InvitedUserRewardPoints;
	}

	public async Task SetInviteTextTemplateAsync(
		string inviteTextTemplate,
		CancellationToken cancellationToken)
	{
		var entity = await GetOrCreateAsync(cancellationToken);
		entity.InviteTextTemplate = inviteTextTemplate;
		await _db.SaveChangesAsync(cancellationToken);
	}

	public async Task SetSettingsAsync(
		string inviteTextTemplate,
		int inviterRewardPoints,
		int invitedUserRewardPoints,
		CancellationToken cancellationToken)
	{
		var entity = await GetOrCreateAsync(cancellationToken);
		entity.InviteTextTemplate = inviteTextTemplate;
		_db.Entry(entity).Property(InviterRewardPointsPropertyName).CurrentValue = inviterRewardPoints;
		_db.Entry(entity).Property(InvitedUserRewardPointsPropertyName).CurrentValue = invitedUserRewardPoints;
		await _db.SaveChangesAsync(cancellationToken);
	}

	public async Task SetInviterRewardPointsAsync(
		int inviterRewardPoints,
		CancellationToken cancellationToken)
	{
		var entity = await GetOrCreateAsync(cancellationToken);
		_db.Entry(entity).Property(InviterRewardPointsPropertyName).CurrentValue = inviterRewardPoints;
		await _db.SaveChangesAsync(cancellationToken);
	}

	public async Task SetInvitedUserRewardPointsAsync(
		int invitedUserRewardPoints,
		CancellationToken cancellationToken)
	{
		var entity = await GetOrCreateAsync(cancellationToken);
		_db.Entry(entity).Property(InvitedUserRewardPointsPropertyName).CurrentValue = invitedUserRewardPoints;
		await _db.SaveChangesAsync(cancellationToken);
	}

	private async Task<ReferralSettingsEntity> GetOrCreateAsync(CancellationToken cancellationToken)
	{
		var entity = await _db.ReferralSettings
			.FirstOrDefaultAsync(x => x.Id == SingletonId, cancellationToken);

		if(entity is not null)
		{
			return entity;
		}

		entity = new ReferralSettingsEntity
		{
			Id = SingletonId,
			InviteTextTemplate = ReferralSettingsDefaults.InviteTextTemplate
		};

		_db.ReferralSettings.Add(entity);
		_db.Entry(entity).Property(InviterRewardPointsPropertyName).CurrentValue = ReferralSettingsDefaults.InviterRewardPoints;
		_db.Entry(entity).Property(InvitedUserRewardPointsPropertyName).CurrentValue = ReferralSettingsDefaults.InvitedUserRewardPoints;

		return entity;
	}
}