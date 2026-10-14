using System.Security.Claims;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Application;
using ProjectManagement.Infrastructure.Persistence;

namespace ProjectManagement.API;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? accessor.HttpContext?.User.FindFirstValue("sub");
    public bool IsAdmin => accessor.HttpContext?.User.IsInRole("Admin") ?? false;
}

public sealed class TaskHub : Hub
{
    private readonly AppDbContext _db;
    public TaskHub(AppDbContext db) => _db = db;
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var projectIds = await _db.ProjectMembers.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.ProjectId).ToListAsync();
            var ownedIds = await _db.Projects.AsNoTracking().Where(p => p.CreatedByUserId == userId).Select(p => p.Id).ToListAsync();
            foreach (var id in projectIds.Concat(ownedIds).Distinct()) await Groups.AddToGroupAsync(Context.ConnectionId, $"project:{id}");
            if (Context.User?.IsInRole("Admin") == true) { var all = await _db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(); foreach (var id in all) await Groups.AddToGroupAsync(Context.ConnectionId, $"project:{id}"); }
        }
        await base.OnConnectedAsync();
    }
}

public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => context.GetHttpContext().User.Identity?.IsAuthenticated == true && context.GetHttpContext().User.IsInRole("Admin");
}

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope(); var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "ProjectManager", "Developer" }) if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>(); var email = config["SeedAdmin:Email"]; var password = config["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = await users.FindByEmailAsync(email);
            if (user is null) { user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Administrator" }; var result = await users.CreateAsync(user, password); if (!result.Succeeded) throw new InvalidOperationException("Seed Admin creation failed: " + string.Join("; ", result.Errors.Select(e => e.Description))); }
            if (!await users.IsInRoleAsync(user, "Admin")) await users.AddToRoleAsync(user, "Admin");
        }
    }
}

public sealed class TaskEventPublisher(Microsoft.AspNetCore.SignalR.IHubContext<TaskHub> hub) : ProjectManagement.Application.ITaskEventPublisher
{
    public Task TaskChangedAsync(Guid projectId, Guid taskId, string eventName, CancellationToken cancellationToken = default) => hub.Clients.Group($"project:{projectId}").SendAsync(eventName, new { projectId, taskId, occurredAtUtc = DateTime.UtcNow }, cancellationToken);
}
