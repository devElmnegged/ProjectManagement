using Hangfire.Dashboard;
namespace ProjectManagement.Infrastructure.BackgroundJobs
{

    public sealed class AllowDevelopmentDashboardAccess
        : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            return true;
        }
    }

    public sealed class HangfireAdminDashboardAuthorizationFilter
        : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            return httpContext.User.Identity?.IsAuthenticated == true
                && httpContext.User.IsInRole("Admin");
        }
    }
}
