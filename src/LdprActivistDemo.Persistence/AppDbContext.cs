using Microsoft.EntityFrameworkCore;

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

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

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
			b.ToTable("users", t => t.HasCheckConstraint("ck_users_points_non_negative", "\"Points\" >= 0"));
			b.HasKey(x => x.Id);

			b.Property(x => x.LastName).IsRequired();
			b.Property(x => x.FirstName).IsRequired();

			b.Property(x => x.PhoneNumber).IsRequired();
			b.HasIndex(x => x.PhoneNumber).IsUnique();

			b.Property(x => x.PasswordHash).IsRequired();

			b.Property(x => x.Points).HasDefaultValue(0);

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
			b.ToTable("tasks", t => t.HasCheckConstraint("ck_tasks_reward_non_negative", "\"RewardPoints\" >= 0"));
			b.HasKey(x => x.Id);

			b.Property(x => x.Title).IsRequired();
			b.Property(x => x.Description).IsRequired();
			b.Property(x => x.RequirementsText).IsRequired();

			b.Property(x => x.RewardPoints).IsRequired();

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
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<TaskSubmission>(b =>
		{
			b.ToTable("task_submissions");
			b.HasKey(x => x.Id);

			b.HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();

			b.HasOne(x => x.Task)
				.WithMany()
				.HasForeignKey(x => x.TaskId)
				.OnDelete(DeleteBehavior.Cascade);

			b.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId)
				.OnDelete(DeleteBehavior.Restrict);

			b.HasOne(x => x.ConfirmedByAdmin)
				.WithMany()
				.HasForeignKey(x => x.ConfirmedByAdminId)
				.OnDelete(DeleteBehavior.Restrict);
		});
	}
}