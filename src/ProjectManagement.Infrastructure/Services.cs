using Autofac;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application;
using ProjectManagement.Domain;
using ProjectManagement.Infrastructure.Persistence;

namespace ProjectManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(config.GetConnectionString("DefaultConnection")));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddMemoryCache();
        services.AddHangfire(c => c.UseSqlServerStorage(config.GetConnectionString("DefaultConnection")));
        services.AddHangfireServer();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<OverdueTaskJob>();
        return services;
    }
    public static void ConfigureContainer(ContainerBuilder builder) { /* Add Autofac registrations here as application services grow. */ }
}

public sealed class LoggingEmailSender(Microsoft.Extensions.Logging.ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    { logger.LogInformation("Email adapter is not configured. To={To}, Subject={Subject}", to, subject); return Task.CompletedTask; }
}


public sealed class OverdueTaskJob(AppDbContext db, IEmailSender email, Microsoft.Extensions.Logging.ILogger<OverdueTaskJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await db.Tasks.Include(t => t.Project).Where(t => t.DueDateUtc < now && t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled && t.OverdueNotificationSentAtUtc == null && t.AssignedToUserId != null).Take(500).ToListAsync(cancellationToken);
        foreach (var task in overdue)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == task.AssignedToUserId, cancellationToken);
            if (user?.Email is null) continue;
            await email.SendAsync(user.Email, $"Overdue task: {task.Title}", $"Task '{task.Title}' in project '{task.Project.Name}' is overdue.", cancellationToken);
            task.OverdueNotificationSentAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Processed {Count} overdue task notifications", overdue.Count);
    }
}
