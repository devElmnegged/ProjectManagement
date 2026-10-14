using FluentValidation.TestHelper;
using ProjectManagement.Application.Projects;
using ProjectManagement.Application.Tasks;
using ProjectManagement.Domain;
using Xunit;

namespace ProjectManagement.UnitTests;

public sealed class ValidationTests
{
    [Fact] public void CreateProject_rejects_empty_name() { var result = new CreateProjectValidator().TestValidate(new CreateProjectCommand(" ", "desc", DateTime.Today, null, ProjectStatus.Planned, 0)); result.ShouldHaveValidationErrorFor(x => x.Name); }
    [Fact] public void CreateProject_rejects_negative_budget() { var result = new CreateProjectValidator().TestValidate(new CreateProjectCommand("Project", "desc", DateTime.Today, null, ProjectStatus.Planned, -1)); result.ShouldHaveValidationErrorFor(x => x.Budget); }
    [Fact] public void Bulk_update_rejects_duplicate_ids() { var id = Guid.NewGuid(); var result = new BulkUpdateWorkTaskStatusValidator().TestValidate(new BulkUpdateWorkTaskStatusCommand(new[] { id, id }, WorkTaskStatus.Completed)); result.ShouldHaveValidationErrorFor(x => x.TaskIds); }
    [Fact] public void Bulk_update_rejects_more_than_100_ids() { var ids = Enumerable.Range(0,101).Select(_ => Guid.NewGuid()).ToArray(); var result = new BulkUpdateWorkTaskStatusValidator().TestValidate(new BulkUpdateWorkTaskStatusCommand(ids, WorkTaskStatus.Completed)); result.ShouldHaveValidationErrorFor(x => x.TaskIds); }
}
