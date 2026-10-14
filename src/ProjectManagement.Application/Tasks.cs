using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain;

namespace ProjectManagement.Application.Tasks;

public sealed record TaskDto(Guid Id, Guid ProjectId, string Title, string Description, string? AssignedToUserId, TaskPriority Priority, DateTime? DueDateUtc, WorkTaskStatus Status, DateTime UpdatedAtUtc);
public sealed record CreateTaskCommand(Guid ProjectId, string Title, string Description, string? AssignedToUserId, TaskPriority Priority, DateTime? DueDateUtc) : IRequest<Guid>;
public sealed record UpdateWorkTaskStatusCommand(Guid Id, WorkTaskStatus Status) : IRequest<bool>;
public sealed record BulkUpdateWorkTaskStatusCommand(IReadOnlyList<Guid> TaskIds, WorkTaskStatus Status) : IRequest<int>;
public sealed record GetTasksQuery(Guid ProjectId, int Page = 1, int PageSize = 20, WorkTaskStatus? Status = null, TaskPriority? Priority = null) : IRequest<IReadOnlyList<TaskDto>>;

public sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator() { RuleFor(x => x.ProjectId).NotEmpty(); RuleFor(x => x.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(4000); }
}
public sealed class BulkUpdateWorkTaskStatusValidator : AbstractValidator<BulkUpdateWorkTaskStatusCommand>
{
    public BulkUpdateWorkTaskStatusValidator() { RuleFor(x => x.TaskIds).NotEmpty().Must(x => x.Count <= 100).WithMessage("A maximum of 100 tasks can be updated per request.").Must(x => x.Distinct().Count() == x.Count).WithMessage("Task IDs must be unique."); }
}

public sealed class CreateTaskHandler(IApplicationDbContext db, ICurrentUser user, ITaskEventPublisher events) : IRequestHandler<CreateTaskCommand, Guid>
{
    public async Task<Guid> Handle(CreateTaskCommand r, CancellationToken ct)
    {
        var project = await db.Projects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == r.ProjectId, ct) ?? throw new KeyNotFoundException("Project not found.");
        if (!user.IsAdmin && project.CreatedByUserId != user.UserId && !project.Members.Any(m => m.UserId == user.UserId)) throw new UnauthorizedAccessException();
        var task = new TaskItem { ProjectId = r.ProjectId, Title = r.Title.Trim(), Description = r.Description.Trim(), AssignedToUserId = r.AssignedToUserId, Priority = r.Priority, DueDateUtc = r.DueDateUtc };
        db.Tasks.Add(task); await db.SaveChangesAsync(ct); await events.TaskChangedAsync(task.ProjectId, task.Id, "TaskCreated", ct); return task.Id;
    }
}
public sealed class UpdateWorkTaskStatusHandler(IApplicationDbContext db, ICurrentUser user, ITaskEventPublisher events) : IRequestHandler<UpdateWorkTaskStatusCommand, bool>
{
    public async Task<bool> Handle(UpdateWorkTaskStatusCommand r, CancellationToken ct)
    {
        var t = await db.Tasks.Include(t => t.Project).ThenInclude(p => p.Members).FirstOrDefaultAsync(t => t.Id == r.Id, ct); if (t is null) return false;
        if (!user.IsAdmin && t.Project.CreatedByUserId != user.UserId && !t.Project.Members.Any(m => m.UserId == user.UserId) && t.AssignedToUserId != user.UserId) throw new UnauthorizedAccessException();
        t.Status = r.Status; t.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); await events.TaskChangedAsync(t.ProjectId, t.Id, "TaskUpdated", ct); return true;
    }
}
public sealed class BulkUpdateWorkTaskStatusHandler(IApplicationDbContext db, ICurrentUser user, ITaskEventPublisher events) : IRequestHandler<BulkUpdateWorkTaskStatusCommand, int>
{
    public async Task<int> Handle(BulkUpdateWorkTaskStatusCommand r, CancellationToken ct)
    {
        var tasks = await db.Tasks.Include(t => t.Project).ThenInclude(p => p.Members).Where(t => r.TaskIds.Contains(t.Id)).ToListAsync(ct);
        if (tasks.Count != r.TaskIds.Count) throw new KeyNotFoundException("One or more tasks were not found.");
        if (!user.IsAdmin && tasks.Any(t => t.Project.CreatedByUserId != user.UserId && !t.Project.Members.Any(m => m.UserId == user.UserId) && t.AssignedToUserId != user.UserId)) throw new UnauthorizedAccessException();
        foreach (var task in tasks) { task.Status = r.Status; task.UpdatedAtUtc = DateTime.UtcNow; }
        // EF Core wraps a single SaveChanges call in a transaction by default.
        await db.SaveChangesAsync(ct);
        foreach (var task in tasks) await events.TaskChangedAsync(task.ProjectId, task.Id, "TaskUpdated", ct);
        return tasks.Count;
    }
}
public sealed class GetTasksHandler(IApplicationDbContext db, ICurrentUser user) : IRequestHandler<GetTasksQuery, IReadOnlyList<TaskDto>>
{
    public async Task<IReadOnlyList<TaskDto>> Handle(GetTasksQuery r, CancellationToken ct)
    {
        var q = db.Tasks.AsNoTracking().Where(t => t.ProjectId == r.ProjectId && (user.IsAdmin || t.Project.CreatedByUserId == user.UserId || t.Project.Members.Any(m => m.UserId == user.UserId) || t.AssignedToUserId == user.UserId));
        if (r.Status.HasValue) q = q.Where(t => t.Status == r.Status); if (r.Priority.HasValue) q = q.Where(t => t.Priority == r.Priority);
        return await q.OrderBy(t => t.DueDateUtc).Skip((Math.Max(1,r.Page)-1)*Math.Clamp(r.PageSize,1,100)).Take(Math.Clamp(r.PageSize,1,100)).Select(t => new TaskDto(t.Id,t.ProjectId,t.Title,t.Description,t.AssignedToUserId,t.Priority,t.DueDateUtc,t.Status,t.UpdatedAtUtc)).ToListAsync(ct);
    }
}
