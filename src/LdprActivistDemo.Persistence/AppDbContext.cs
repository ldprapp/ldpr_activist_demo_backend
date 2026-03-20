using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Tasks;

using Microsoft.EntityFrameworkCore;

using TaskStatusValues = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Persistence;

public sealed class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options)
		: base(options)
	{
	}

	public DbSet<Region> Regions => Set<Region>();
	public DbSet<Settlement> Settlements => Set<Settlement>();
	public DbSet<User> Users => Set<User>();
	public DbSet<UserRating> UserRatings => Set<UserRating>();
	public DbSet<UserRatingsRefreshState> UserRatingsRefreshStates => Set<UserRatingsRefreshState>();
	public DbSet<UserPointsTransaction> UserPointsTransactions => Set<UserPointsTransaction>();
	public DbSet<SystemImageEntity> SystemImages => Set<SystemImageEntity>();

	public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
	public DbSet<TaskTrustedCoordinator> TaskTrustedCoordinators => Set<TaskTrustedCoordinator>();
	public DbSet<TaskSubmission> TaskSubmissions => Set<TaskSubmission>();
	public DbSet<TaskSubmissionImage> TaskSubmissionImages => Set<TaskSubmissionImage>();
	public DbSet<ImageEntity> Images => Set<ImageEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<ImageEntity>(b =>
		{
			b.ToTable("images");
			b.HasKey(x => x.Id);
			b.Property(x => x.OwnerUserId).IsRequired();
			b.Property(x => x.ContentType).IsRequired();
			b.Property(x => x.Data).IsRequired();
			b.HasIndex(x => x.OwnerUserId);

			b.HasOne(x => x.OwnerUser)
				.WithMany(x => x.OwnedImages)
				.HasForeignKey(x => x.OwnerUserId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<SystemImageEntity>(b =>
		{
			b.ToTable("system_images");
			b.HasKey(x => x.Id);
			b.Property(x => x.ImageId).IsRequired();
			b.Property(x => x.Name).IsRequired();
			b.HasIndex(x => x.Name).IsUnique();

			b.HasOne(x => x.Image)
				.WithMany()
				.HasForeignKey(x => x.ImageId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<Region>(b =>
		{
			b.ToTable("regions");
			b.HasKey(x => x.Id);
			b.Property(x => x.Name).IsRequired();
			b.HasIndex(x => x.Name).IsUnique();
		});

		modelBuilder.Entity<Settlement>(b =>
		{
			b.ToTable("settlements");
			b.HasKey(x => x.Id);
			b.Property(x => x.Name).IsRequired();
			b.Property(x => x.IsDeleted)
				.IsRequired()
				.HasDefaultValue(false);

			b.HasOne(x => x.Region)
				.WithMany(r => r.Settlements)
				.HasForeignKey(x => x.RegionId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasIndex(x => new { x.RegionId, x.Name }).IsUnique();
		});

		modelBuilder.Entity<User>(b =>
		{
			b.ToTable("users", t =>
			{
				t.HasCheckConstraint(
					"ck_users_gender_allowed",
					"\"Gender\" IS NULL OR \"Gender\" IN ('male','female')");
				t.HasCheckConstraint(
					"ck_users_role_allowed",
					$"\"Role\" IN ('{UserRoles.Activist}','{UserRoles.Coordinator}','{UserRoles.Admin}')");
			});
			b.HasKey(x => x.Id);

			b.Property(x => x.LastName).IsRequired();
			b.Property(x => x.FirstName).IsRequired();

			b.Property(x => x.PhoneNumber).IsRequired();
			b.Property(x => x.Role)
				.IsRequired()
				.HasDefaultValue(UserRoles.Activist);
			b.HasIndex(x => x.PhoneNumber).IsUnique();

			b.Property(x => x.PasswordHash).IsRequired();

			b.Property(x => x.AvatarImageUrl);
			b.Navigation(x => x.OwnedImages);

			b.HasOne(x => x.Region)
				.WithMany()
				.HasForeignKey(x => x.RegionId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasOne(x => x.Settlement)
				.WithMany()
				.HasForeignKey(x => x.SettlementId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<UserRating>(b =>
		{
			b.ToTable("user_ratings");
			b.HasKey(x => x.UserId);

			b.Property(x => x.UserId)
				.ValueGeneratedNever();

			b.Property(x => x.OverallRank);
			b.Property(x => x.RegionRank);
			b.Property(x => x.SettlementRank);

			b.HasOne(x => x.User)
				.WithOne()
				.HasForeignKey<UserRating>(x => x.UserId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<UserRatingsRefreshState>(b =>
		{
			b.ToTable("user_ratings_refresh_state", t =>
			{
				t.HasCheckConstraint(
					"ck_user_ratings_refresh_state_hour_range",
					"\"ScheduledHour\" >= 0 AND \"ScheduledHour\" <= 23");
				t.HasCheckConstraint(
					"ck_user_ratings_refresh_state_minute_range",
					"\"ScheduledMinute\" >= 0 AND \"ScheduledMinute\" <= 59");
			});
			b.HasKey(x => x.JobName);
			b.Property(x => x.JobName).IsRequired();
			b.Property(x => x.ScheduledHour)
				.IsRequired()
				.HasDefaultValue(4);
			b.Property(x => x.ScheduledMinute)
				.IsRequired()
				.HasDefaultValue(0);
			b.Property(x => x.LastCompletedLocalDate);
			b.Property(x => x.LastCompletedAtUtc);
		});

		modelBuilder.Entity<UserPointsTransaction>(b =>
		{
			b.ToTable("user_points_transactions");
			b.HasKey(x => x.Id);

			b.Property(x => x.UserId).IsRequired();
			b.Property(x => x.Amount).IsRequired();
			b.Property(x => x.TransactionAt).IsRequired();
			b.Property(x => x.Comment).IsRequired();
			b.Property(x => x.CoordinatorUserId);
			b.Property(x => x.TaskId);

			b.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasOne(x => x.CoordinatorUser)
				.WithMany()
				.HasForeignKey(x => x.CoordinatorUserId)
				.OnDelete(DeleteBehavior.SetNull);

			b.HasOne(x => x.Task)
				.WithMany()
				.HasForeignKey(x => x.TaskId)
				.OnDelete(DeleteBehavior.SetNull);

			b.HasIndex(x => new { x.UserId, x.TransactionAt });
			b.HasIndex(x => x.CoordinatorUserId);
			b.HasIndex(x => x.TaskId);
		});

		modelBuilder.Entity<TaskEntity>(b =>
		{
			b.ToTable("tasks", t =>
			{
				t.HasCheckConstraint(
					"ck_tasks_reward_non_negative",
					"\"RewardPoints\" >= 0");

				t.HasCheckConstraint(
					"ck_tasks_status_allowed",
					$"\"Status\" IN ('{TaskStatusValues.Open}','{TaskStatusValues.Closed}')");

				t.HasCheckConstraint(
				"ck_tasks_verification_type_allowed",
				$"\"VerificationType\" IN ('{TaskVerificationType.Auto}','{TaskVerificationType.Manual}')");

				t.HasCheckConstraint(
					"ck_tasks_reuse_type_allowed",
					$"\"ReuseType\" IN ('{TaskReuseType.Disposable}','{TaskReuseType.Reusable}')");

				t.HasCheckConstraint(
					"ck_tasks_auto_verification_action_type_allowed",
					$"(\"VerificationType\" = '{TaskVerificationType.Manual}' AND \"AutoVerificationActionType\" IS NULL) OR (\"VerificationType\" = '{TaskVerificationType.Auto}' AND \"AutoVerificationActionType\" IN ('{TaskAutoVerificationActionType.InviteFriend}','{TaskAutoVerificationActionType.FirstLogin}','{TaskAutoVerificationActionType.Auto}'))");
			});
			b.HasKey(x => x.Id);

			b.Property(x => x.Title).IsRequired();
			b.Property(x => x.Description).IsRequired();
			b.Property(x => x.RequirementsText).IsRequired();

			b.Property(x => x.RewardPoints).IsRequired();

			b.Property(x => x.Status).IsRequired();

			b.Property(x => x.VerificationType)
				.IsRequired()
				.HasDefaultValue(TaskVerificationType.Manual);

			b.Property(x => x.ReuseType)
				.IsRequired()
				.HasDefaultValue(TaskReuseType.Disposable);

			b.Property(x => x.AutoVerificationActionType);

			b.Property(x => x.CoverImageId);
			b.HasOne<ImageEntity>()
				.WithMany()
				.HasForeignKey(x => x.CoverImageId)
				.OnDelete(DeleteBehavior.SetNull);

			b.HasOne(x => x.AuthorUser)
				.WithMany()
				.HasForeignKey(x => x.AuthorUserId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasOne(x => x.Region)
				.WithMany()
				.HasForeignKey(x => x.RegionId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasOne(x => x.Settlement)
				.WithMany()
				.HasForeignKey(x => x.SettlementId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasIndex(x => new { x.RegionId, x.SettlementId, x.Status, x.DeadlineAt });
		}); modelBuilder.Entity<TaskTrustedCoordinator>(b =>
		{
			b.ToTable("task_trusted_coordinators");
			b.HasKey(x => new { x.TaskId, x.CoordinatorUserId });
			b.HasOne(x => x.Task)
				.WithMany(t => t.TrustedCoordinators)
				.HasForeignKey(x => x.TaskId)
				.OnDelete(DeleteBehavior.Cascade);
			b.HasOne(x => x.CoordinatorUser)
				.WithMany()
				.HasForeignKey(x => x.CoordinatorUserId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<TaskSubmission>(b =>
		{
			b.ToTable("task_submissions", t =>
			{
				t.HasCheckConstraint(
					"ck_task_submissions_decision_status_allowed",
					$"\"DecisionStatus\" IN ('{TaskSubmissionDecisionStatus.InProgress}','{TaskSubmissionDecisionStatus.SubmittedForReview}','{TaskSubmissionDecisionStatus.Approve}','{TaskSubmissionDecisionStatus.Rejected}')");
			});
			b.HasKey(x => x.Id);

			b.Property(x => x.DecisionStatus)
			   .IsRequired()
			   .HasDefaultValue(TaskSubmissionDecisionStatus.InProgress);

			b.HasIndex(x => new { x.TaskId, x.UserId });

			b.HasOne(x => x.Task)
				.WithMany()
				.HasForeignKey(x => x.TaskId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId)
				.OnDelete(DeleteBehavior.Cascade); b.HasOne(x => x.DecidedByCoordinator)
				.WithMany()
				.HasForeignKey(x => x.DecidedByCoordinatorId)
				.OnDelete(DeleteBehavior.SetNull);
		});

		modelBuilder.Entity<TaskSubmissionImage>(b =>
		{
			b.ToTable("task_submission_images");
			b.HasKey(x => new { x.SubmissionId, x.ImageId });

			b.HasOne(x => x.Submission)
				.WithMany(s => s.PhotoImages)
				.HasForeignKey(x => x.SubmissionId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasOne(x => x.Image)
				.WithMany()
				.HasForeignKey(x => x.ImageId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasIndex(x => x.ImageId);
		});
	}

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
		=> SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None).GetAwaiter().GetResult();

	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		=> SaveChangesAsync(true, cancellationToken);

	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
		=> base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
}