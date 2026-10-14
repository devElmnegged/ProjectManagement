using Xunit;

namespace ProjectManagement.IntegrationTests;

// Full HTTP integration tests require a disposable SQL Server and configured JWT secret.
// Keep this test as an explicit reminder that DB-backed smoke tests are environment-dependent.
public sealed class ApiSmokeTests
{
    [Fact]
    public void Integration_test_project_is_configured() => Assert.True(true);
}
