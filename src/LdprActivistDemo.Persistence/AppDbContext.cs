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
	public DbSet<City> Cities => Set<City>();
	public DbSet<User> Users => Set<User>();

	public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
	public DbSet<TaskTrustedAdmin> TaskTrustedAdmins => Set<TaskTrustedAdmin>();
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
			b.Property(x => x.ContentType).IsRequired();
			b.Property(x => x.Data).IsRequired();
		});

		modelBuilder.Entity<Region>(b =>
		{
			b.ToTable("regions");
			b.HasKey(x => x.Id);
			b.Property(x => x.Name).IsRequired();
			b.HasIndex(x => x.Name).IsUnique();
		});

		modelBuilder.Entity<City>(b =>
		{
			b.ToTable("cities");
			b.HasKey(x => x.Id);
			b.Property(x => x.Name).IsRequired();

			b.HasOne(x => x.Region)
				.WithMany(r => r.Cities)
				.HasForeignKey(x => x.RegionId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasIndex(x => new { x.RegionId, x.Name }).IsUnique();
		});

		modelBuilder.Entity<User>(b =>
		{
			b.ToTable("users", t =>
			{
				t.HasCheckConstraint("ck_users_points_non_negative", "\"Points\" >= 0");
				t.HasCheckConstraint(
					"ck_users_gender_allowed",
					"\"Gender\" IS NULL OR \"Gender\" IN ('male','female')");
			});
			b.HasKey(x => x.Id);

			b.Property(x => x.LastName).IsRequired();
			b.Property(x => x.FirstName).IsRequired();

			b.Property(x => x.PhoneNumber).IsRequired();
			b.HasIndex(x => x.PhoneNumber).IsUnique();

			b.Property(x => x.PasswordHash).IsRequired();

			b.Property(x => x.Points).HasDefaultValue(0);

			b.Property(x => x.AvatarImageUrl);

			b.HasOne(x => x.Region)
				.WithMany()
				.HasForeignKey(x => x.RegionId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasOne(x => x.City)
				.WithMany()
				.HasForeignKey(x => x.CityId)
				.OnDelete(DeleteBehavior.Restrict);
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

			b.HasOne(x => x.City)
				.WithMany()
				.HasForeignKey(x => x.CityId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasIndex(x => new { x.RegionId, x.CityId, x.Status, x.DeadlineAt });
		});

		modelBuilder.Entity<TaskTrustedAdmin>(b =>
		{
			b.ToTable("task_trusted_admins");
			b.HasKey(x => new { x.TaskId, x.AdminUserId });

			b.HasOne(x => x.Task)
				.WithMany(t => t.TrustedAdmins)
				.HasForeignKey(x => x.TaskId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasOne(x => x.AdminUser)
				.WithMany()
				.HasForeignKey(x => x.AdminUserId)
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

			b.HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();

			b.HasOne(x => x.Task)
				.WithMany()
				.HasForeignKey(x => x.TaskId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasOne(x => x.DecidedByAdmin)
				.WithMany()
				.HasForeignKey(x => x.DecidedByAdminId)
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