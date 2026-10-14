using ProjectManagement.Domain;

namespace ProjectManagement.Application;

public interface IApplicationDbContext
{
    Microsoft.EntityFrameworkCore.DbSet<Project> Projects { get; }
    Microsoft.EntityFrameworkCore.DbSet<ProjectMember> ProjectMembers { get; }
    Microsoft.EntityFrameworkCore.DbSet<TaskItem> Tasks { get; }
    Microsoft.EntityFrameworkCore.DbSet<UserNotification> Notifications { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsAdmin { get; }
}

public interface ITaskEventPublisher
{
    Task TaskChangedAsync(Guid projectId, Guid taskId, string eventName, CancellationToken cancellationToken = default);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
