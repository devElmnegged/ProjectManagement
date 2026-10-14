using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Application.Tasks;

namespace ProjectManagement.API.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/tasks"), Authorize]
public sealed class TasksController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetTasksQuery query, CancellationToken ct) => Ok(await sender.Send(query, ct));
    [HttpPost]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Create(CreateTaskCommand command, CancellationToken ct) { var id = await sender.Send(command, ct); return Created($"/api/v1/tasks/{id}", new { id }); }
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] StatusRequest request, CancellationToken ct) => await sender.Send(new UpdateWorkTaskStatusCommand(id, request.Status), ct) ? NoContent() : NotFound();
    [HttpPatch("bulk-status")]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> BulkStatus(BulkUpdateWorkTaskStatusCommand command, CancellationToken ct) => Ok(new { updated = await sender.Send(command, ct) });
    public sealed record StatusRequest(ProjectManagement.Domain.WorkTaskStatus Status);
}
