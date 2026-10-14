using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain;

namespace ProjectManagement.Application.Projects;

public sealed record ProjectDto(Guid Id, string Name, string Description, DateTime StartDate, DateTime? EndDate, ProjectStatus Status, decimal Budget, string CreatedByUserId);
public sealed record ProjectPage(IReadOnlyList<ProjectDto> Items, int Page, int PageSize, int TotalCount);
public sealed record CreateProjectCommand(string Name, string Description, DateTime StartDate, DateTime? EndDate, ProjectStatus Status, decimal Budget) : IRequest<Guid>;
public sealed record UpdateProjectCommand(Guid Id, string Name, string Description, DateTime StartDate, DateTime? EndDate, ProjectStatus Status, decimal Budget) : IRequest<bool>;
public sealed record DeleteProjectCommand(Guid Id) : IRequest<bool>;
public sealed record GetProjectsQuery(int Page = 1, int PageSize = 20, string? Search = null, string SortBy = "createdAtUtc", bool Descending = true, ProjectStatus? Status = null) : IRequest<ProjectPage>;
public sealed record GetProjectByIdQuery(Guid Id) : IRequest<ProjectDto?>;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(160); RuleFor(x => x.Description).MaximumLength(4000); RuleFor(x => x.Budget).GreaterThanOrEqualTo(0); RuleFor(x => x.EndDate).Must((x, end) => end is null || end.Value.Date >= x.StartDate.Date).WithMessage("EndDate must be on or after StartDate."); }
}
public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(160); RuleFor(x => x.Description).MaximumLength(4000); RuleFor(x => x.Budget).GreaterThanOrEqualTo(0); RuleFor(x => x.EndDate).Must((x, end) => end is null || end.Value.Date >= x.StartDate.Date); }
}

public sealed class CreateProjectHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<CreateProjectCommand, Guid>
{
    public async Task<Guid> Handle(CreateProjectCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) throw new UnauthorizedAccessException();
        var p = new Project { Name = r.Name.Trim(), Description = r.Description.Trim(), StartDate = r.StartDate, EndDate = r.EndDate, Status = r.Status, Budget = r.Budget, CreatedByUserId = currentUser.UserId };
        p.Members.Add(new ProjectMember { ProjectId = p.Id, UserId = currentUser.UserId });
        db.Projects.Add(p); await db.SaveChangesAsync(ct); return p.Id;
    }
}
public sealed class UpdateProjectHandler(IApplicationDbContext db, ICurrentUser user) : IRequestHandler<UpdateProjectCommand, bool>
{
    public async Task<bool> Handle(UpdateProjectCommand r, CancellationToken ct)
    {
        var p = await db.Projects.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (p is null) return false;
        if (!user.IsAdmin && !p.Members.Any(m => m.UserId == user.UserId) && p.CreatedByUserId != user.UserId) throw new UnauthorizedAccessException();
        p.Name = r.Name.Trim(); p.Description = r.Description.Trim(); p.StartDate = r.StartDate; p.EndDate = r.EndDate; p.Status = r.Status; p.Budget = r.Budget; p.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return true;
    }
}
public sealed class DeleteProjectHandler(IApplicationDbContext db, ICurrentUser user) : IRequestHandler<DeleteProjectCommand, bool>
{
    public async Task<bool> Handle(DeleteProjectCommand r, CancellationToken ct)
    {
        var p = await db.Projects.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == r.Id, ct); if (p is null) return false;
        if (!user.IsAdmin && p.CreatedByUserId != user.UserId) throw new UnauthorizedAccessException();
        db.Projects.Remove(p); await db.SaveChangesAsync(ct); return true;
    }
}
public sealed class GetProjectsHandler(IApplicationDbContext db, ICurrentUser user) : IRequestHandler<GetProjectsQuery, ProjectPage>
{
    public async Task<ProjectPage> Handle(GetProjectsQuery r, CancellationToken ct)
    {
        var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 100);
        IQueryable<Project> q = db.Projects.AsNoTracking();
        if (!user.IsAdmin) q = q.Where(p => p.CreatedByUserId == user.UserId || p.Members.Any(m => m.UserId == user.UserId));
        if (!string.IsNullOrWhiteSpace(r.Search)) { var term = r.Search.Trim(); q = q.Where(p => p.Name.Contains(term) || p.Description.Contains(term)); }
        if (r.Status.HasValue) q = q.Where(p => p.Status == r.Status.Value);
        q = r.SortBy.ToLowerInvariant() switch { "name" => r.Descending ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name), "startdate" => r.Descending ? q.OrderByDescending(p => p.StartDate) : q.OrderBy(p => p.StartDate), "budget" => r.Descending ? q.OrderByDescending(p => p.Budget) : q.OrderBy(p => p.Budget), _ => r.Descending ? q.OrderByDescending(p => p.CreatedAtUtc) : q.OrderBy(p => p.CreatedAtUtc) };
        var total = await q.CountAsync(ct); var items = await q.Skip((page - 1) * size).Take(size).Select(p => new ProjectDto(p.Id,p.Name,p.Description,p.StartDate,p.EndDate,p.Status,p.Budget,p.CreatedByUserId)).ToListAsync(ct);
        return new ProjectPage(items, page, size, total);
    }
}
public sealed class GetProjectByIdHandler(IApplicationDbContext db, ICurrentUser user) : IRequestHandler<GetProjectByIdQuery, ProjectDto?>
{
    public async Task<ProjectDto?> Handle(GetProjectByIdQuery r, CancellationToken ct)
    {
        var p = await db.Projects.AsNoTracking().Where(p => p.Id == r.Id && (user.IsAdmin || p.CreatedByUserId == user.UserId || p.Members.Any(m => m.UserId == user.UserId))).Select(p => new ProjectDto(p.Id,p.Name,p.Description,p.StartDate,p.EndDate,p.Status,p.Budget,p.CreatedByUserId)).FirstOrDefaultAsync(ct); return p;
    }
}
