// Simple test to verify ITestHarness registration
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using WorkerService.IntegrationTests.InMemory.Fixtures;
using Xunit;

[Fact]
public void TestHarnessIsRegistered()
{
    var factory = new InMemoryWebApplicationFactory();
    using var scope = factory.Services.CreateScope();
    var harness = scope.ServiceProvider.GetService<ITestHarness>();
    Assert.NotNull(harness);
}