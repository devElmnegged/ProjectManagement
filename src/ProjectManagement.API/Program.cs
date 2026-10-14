using System.Text;
using Asp.Versioning;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProjectManagement.Application;
using ProjectManagement.Domain;
using ProjectManagement.Infrastructure;
using ProjectManagement.Infrastructure.Persistence;
using ProjectManagement.API;
using ProjectManagement.Infrastructure.BackgroundJobs;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>((_, container) => ProjectManagement.Infrastructure.DependencyInjection.ConfigureContainer(container));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityCore<ApplicationUser>(o => { o.Password.RequiredLength = 12; o.Password.RequireDigit = true; o.Password.RequireUppercase = true; o.Password.RequireNonAlphanumeric = true; o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); o.User.RequireUniqueEmail = true; }).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager();
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key must be configured using user-secrets or environment variables.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = jwt["Issuer"], ValidateAudience = true, ValidAudience = jwt["Audience"], ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier, RoleClaimType = System.Security.Claims.ClaimTypes.Role }; o.Events = new JwtBearerEvents { OnMessageReceived = context => { var token = context.Request.Query["access_token"]; if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/tasks")) context.Token = token; return Task.CompletedTask; } }; });
builder.Services.AddAuthorization(o => { o.AddPolicy("AdminOnly", p => p.RequireRole("Admin")); });
builder.Services.AddControllers(); builder.Services.AddSignalR(); builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = StatusCodes.Status429TooManyRequests; o.AddFixedWindowLimiter("api", x => { x.PermitLimit = 120; x.Window = TimeSpan.FromMinutes(1); x.QueueLimit = 0; }); });
builder.Services.AddApiVersioning(o => { o.DefaultApiVersion = new ApiVersion(1,0); o.AssumeDefaultVersionWhenUnspecified = true; o.ReportApiVersions = true; }).AddMvc().AddApiExplorer(o => { o.GroupNameFormat = "'v'VVV"; o.SubstituteApiVersionInUrl = true; });
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen(o => { o.SwaggerDoc("v1", new OpenApiInfo { Title = "Project Management API", Version = "v1" }); o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header }); o.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } }); });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ProjectManagement.Application.ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ProjectManagement.Application.ITaskEventPublisher, TaskEventPublisher>();
var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context => { var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>(); var ex = feature?.Error; context.Response.ContentType = "application/problem+json"; context.Response.StatusCode = ex switch { FluentValidation.ValidationException => 400, UnauthorizedAccessException => 403, KeyNotFoundException => 404, _ => 500 }; await context.Response.WriteAsJsonAsync(new { title = context.Response.StatusCode == 500 ? "An unexpected error occurred." : ex?.Message, status = context.Response.StatusCode }); }));
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers().RequireRateLimiting("api"); app.MapHub<TaskHub>("/hubs/tasks").RequireAuthorization();
var dashboardOptions = new DashboardOptions
{
    Authorization = app.Environment.IsDevelopment()
        ? new[] { new AllowDevelopmentDashboardAccess() }
        : new[] { new HangfireAdminDashboardAuthorizationFilter() }
};

app.UseAuthentication();
app.UseAuthorization();


app.UseHangfireDashboard("/hangfire", dashboardOptions);
RecurringJob.AddOrUpdate<OverdueTaskJob>("overdue-task-notifications", job => job.RunAsync(CancellationToken.None), Cron.Daily(7));
await SeedData.InitializeAsync(app.Services);
app.Run();

public partial class Program { }
