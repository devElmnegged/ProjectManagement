# Project Management API (.NET 8)

Clean Architecture starter for project/task management using ASP.NET Core Web API, EF Core 8, SQL Server, MediatR CQRS, FluentValidation, ASP.NET Core Identity/JWT, SignalR, Hangfire, Autofac, API versioning, and in-memory caching primitives.

> **Important:** This repository is a strong starter, not a security-audited production system. Configure an actual email provider, review role assignment and account lifecycle policies, add database migrations, and run tests before production use. Do not commit secrets.

## Requirements

- .NET 8 SDK
- SQL Server 2019+ or SQL Server Express / LocalDB
- Visual Studio 2022 (optional)
- Postman (optional)

## SQL Server configuration (Windows)

1. Ensure the SQL Server service is running and the instance accepts the connection string you intend to use.
2. Edit `src/ProjectManagement.API/appsettings.json` or preferably configure environment variables / user-secrets.
3. Example for the default local instance:

   `Server=localhost;Database=ProjectManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True`

   For SQL Express, use `Server=localhost\SQLEXPRESS;...`. For LocalDB, use `Server=(localdb)\MSSQLLocalDB;...`.
4. Configure a JWT signing key of at least 32 bytes. From the API project directory:

   ```powershell
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:Key" "PUT_A_RANDOM_SECRET_OF_AT_LEAST_32_BYTES_HERE"
   dotnet user-secrets set "SeedAdmin:Email" "admin@example.com"
   dotnet user-secrets set "SeedAdmin:Password" "Use-A-Unique-Strong-Password!123"
   ```

   User Secrets are for local development. In production, use your hosting platform's secret store or environment variables.

## Create the database

Install the EF tool if needed:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.11
```

From the repository root, create and apply the first migration:

```powershell
dotnet ef migrations add InitialCreate --project src/ProjectManagement.Infrastructure --startup-project src/ProjectManagement.API --output-dir Persistence/Migrations
dotnet ef database update --project src/ProjectManagement.Infrastructure --startup-project src/ProjectManagement.API
```

Migrations are deliberately generated against your local configured SQL Server so that the provider and connection string are explicit.

## Build and run

```powershell
dotnet restore ProjectManagement.sln
dotnet build ProjectManagement.sln
dotnet test ProjectManagement.sln
dotnet run --project src/ProjectManagement.API
```

Open the HTTPS URL printed by the application and append `/swagger` in Development. If the development HTTPS certificate is not trusted, run `dotnet dev-certs https --trust`.

## Initial Admin and roles

At startup, the app creates `Admin`, `ProjectManager`, and `Developer` roles. If `SeedAdmin:Email` and `SeedAdmin:Password` are configured, it creates that Admin user if needed and assigns the Admin role. Keep these values out of source control. Public registration creates Developer users only; role elevation must be handled by an Admin-only workflow before production.

## Main API routes

All routes are under `/api/v1`.

- `POST /auth/register` — create a Developer account
- `POST /auth/login` — receive JWT
- `GET /projects?page=1&pageSize=20&search=portal&sortBy=name&descending=false&status=1`
- `POST /projects` — Admin or ProjectManager
- `GET /projects/{id}`
- `PUT /projects/{id}` and `DELETE /projects/{id}`
- `GET /tasks?projectId={guid}&page=1&pageSize=20`
- `POST /tasks` — Admin or ProjectManager
- `PATCH /tasks/{id}/status`
- `PATCH /tasks/bulk-status` — Admin or ProjectManager; maximum 100 unique IDs
- `GET /hubs/tasks` — authenticated SignalR hub; project updates are broadcast to project groups
- `/hangfire` — Hangfire dashboard, Admin only

Enum values are numeric by default: ProjectStatus `Planned=0, Active=1, OnHold=2, Completed=3, Cancelled=4`; TaskPriority `Low=0, Medium=1, High=2`; TaskStatus `Todo=0, InProgress=1, Blocked=2, Completed=3, Cancelled=4`.

## Postman

Import `postman/ProjectManagement.postman_collection.json`. Set `baseUrl` to the HTTPS URL from `dotnet run`. Register and login, then run the project/task requests. The Login request saves the returned token automatically. Creating projects requires Admin or ProjectManager; new registrations are Developers by default, so configure the initial Admin and use it for project creation.

## SignalR

Connect to `/hubs/tasks` with the bearer token. The server adds authenticated users to groups for projects they own or are members of. Clients receive `TaskCreated` and `TaskUpdated` events. For a production deployment, consider a SignalR backplane when running multiple API instances.

## Hangfire and email

Hangfire uses SQL Server storage and schedules `OverdueTaskJob` daily at 07:00 server time. The included email adapter logs that email delivery is not configured; replace `LoggingEmailSender` with a provider such as SMTP or a transactional email API. The job tracks `OverdueNotificationSentAtUtc` to reduce duplicate notices. Confirm the app's time zone and operational retry policy before deployment.

## Docker

The included Dockerfile builds the API. `docker-compose.yml` assumes SQL Server is reachable from the container via `host.docker.internal`; set `SQL_PASSWORD`, `JWT_KEY`, `SEED_ADMIN_EMAIL`, and `SEED_ADMIN_PASSWORD` in a local untracked `.env` file. This is an optional deployment path; the primary development instructions use locally installed SQL Server.

```powershell
Copy-Item .env.example .env
# Edit .env and replace all placeholder secrets
docker compose up --build
```

Apply migrations from a machine that has the .NET SDK and can reach the database, or add a controlled migration step to your deployment pipeline. Do not run schema migrations blindly on production startup.

## Security and production checklist

- Enforce HTTPS behind correctly configured trusted proxies; set restrictive CORS origins.
- Use a secret manager for JWT signing keys and admin credentials; rotate keys and define token revocation/refresh policy.
- Add email verification, password reset, lockout attempt accounting, account recovery, and admin-only role/member management endpoints.
- Add project-member management endpoints and validate assignee membership before assigning tasks.
- Review authorization policies for each route and every SignalR group membership path. Admin dashboard access must be protected by authentication and role claims.
- Add structured request logging with sensitive-data redaction, health checks, metrics, and alerting.
- Configure an actual email provider and test retry/idempotency behavior.
- Add integration tests against SQL Server (for example Testcontainers) for transaction, EF translation, and cross-project authorization scenarios.
- Review rate limiting, request body size limits, CORS, HSTS, dependency vulnerabilities, and OWASP API Security risks.

## Architecture notes / known scope

- Domain entities and application use cases are separated into projects; MediatR handles commands/queries and FluentValidation is registered as a pipeline behavior.
- EF Core queries use server-side filtering, sorting, projections, paging, and `AsNoTracking` for reads.
- The Unit of Work is EF Core's `SaveChangesAsync` and transaction support. A repository abstraction can be introduced for domain-specific persistence; avoid a generic wrapper that simply duplicates `DbSet`.
- `IMemoryCache` is registered, but user-specific query caching is not enabled by default because invalidation and authorization isolation must be implemented carefully before caching personalized results.
- The included API is a starter implementation. Review compile/build output and complete the production checklist before publishing as production-ready.

## Runing The Project 

```
   From Package Manager 
   run : Update-database 
   then run the project and gor to swagger 
   /swagger 
   Login From Swagger Endpoint 
   Email: Admin@project.com
   Password: Admin_P@ssw0rd2026
```
## For Hangfire Tasks 
``` 
   /hangfire
```
