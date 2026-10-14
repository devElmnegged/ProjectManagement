namespace ProjectManagement.Domain;

public enum ProjectStatus { Planned, Active, OnHold, Completed, Cancelled }
public enum WorkTaskStatus { Todo, InProgress, Blocked, Completed, Cancelled }
public enum TaskPriority { Low, Medium, High }

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public decimal Budget { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

public sealed class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDateUtc { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Todo;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? OverdueNotificationSentAtUtc { get; set; }
}

public sealed class UserNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
