using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Application;
using ProjectManagement.Domain;

namespace ProjectManagement.Infrastructure.Persistence;

public sealed class ApplicationUser : IdentityUser { public string DisplayName { get; set; } = string.Empty; }

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Project>(e => { e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(160).IsRequired(); e.Property(x => x.Description).HasMaxLength(4000); e.Property(x => x.Budget).HasPrecision(18,2); e.HasIndex(x => new { x.Status, x.CreatedAtUtc }); e.HasMany(x => x.Tasks).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<ProjectMember>(e => { e.HasKey(x => new { x.ProjectId, x.UserId }); e.HasOne(x => x.Project).WithMany(x => x.Members).HasForeignKey(x => x.ProjectId); e.HasIndex(x => x.UserId); });
        b.Entity<TaskItem>(e => { e.HasKey(x => x.Id); e.Property(x => x.Title).HasMaxLength(200).IsRequired(); e.Property(x => x.Description).HasMaxLength(4000); e.HasIndex(x => new { x.ProjectId, x.Status, x.DueDateUtc }); e.HasIndex(x => new { x.Status, x.DueDateUtc, x.OverdueNotificationSentAtUtc }); });
        b.Entity<UserNotification>(e => { e.HasKey(x => x.Id); e.Property(x => x.Message).HasMaxLength(1000); e.HasIndex(x => new { x.UserId, x.CreatedAtUtc }); });
    }
}
