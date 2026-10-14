using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ProjectManagement.Application.Projects;

namespace ProjectManagement.API.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/projects"), Authorize]
public sealed class ProjectsController(ISender sender, IMemoryCache cache) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProjectPage>> Get([FromQuery] GetProjectsQuery query, CancellationToken ct) => Ok(await sender.Send(query, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) { var p = await sender.Send(new GetProjectByIdQuery(id), ct); return p is null ? NotFound() : Ok(p); }
    [HttpPost]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Create(CreateProjectCommand command, CancellationToken ct) { var id = await sender.Send(command, ct); return CreatedAtAction(nameof(GetById), new { version = "1.0", id }, new { id }); }
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectCommand command, CancellationToken ct) => id != command.Id ? BadRequest(new { message = "Route id and body id must match." }) : (await sender.Send(command, ct) ? NoContent() : NotFound());
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,ProjectManager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await sender.Send(new DeleteProjectCommand(id), ct) ? NoContent() : NotFound();
}
