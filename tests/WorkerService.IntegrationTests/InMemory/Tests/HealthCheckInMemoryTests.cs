using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using WorkerService.IntegrationTests.InMemory.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WorkerService.IntegrationTests.InMemory.Tests;

[Collection("InMemory Integration Tests")]
public class HealthCheckInMemoryTests : IClassFixture<InMemoryWebApplicationFactory>
{
    private readonly InMemoryWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private HttpClient? _client;

    public HealthCheckInMemoryTests(InMemoryWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = _factory.CreateClient();
    }    

    [Fact]
    public async Task Should_Return_Healthy_Status_When_All_Dependencies_Are_Available()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");

        _output.WriteLine($"Health check response: {content}");
    }    

    [Fact]
    public async Task Should_Execute_Health_Checks_Within_Timeout()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        stopwatch.Stop();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Health checks should complete within reasonable time (1 seconds)
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "Health checks should complete quickly");

        _output.WriteLine($"Health check completed in {stopwatch.ElapsedMilliseconds}ms");
    }    

    [Fact]
    public async Task Should_Verify_MassTransit_Connectivity_Through_Health_Check()
    {
        // This test specifically verifies that the MassTransit health check
        // can actually connect to the test MassTransit instance

        // Arrange
        using var scope = _factory!.Services.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);

        var massTransitHealthCheck = result.Entries.FirstOrDefault(e =>
            e.Key.Contains("masstransit", StringComparison.CurrentCultureIgnoreCase));

        massTransitHealthCheck.Should().NotBeNull();
        massTransitHealthCheck.Value.Status.Should().Be(HealthStatus.Healthy);

        _output.WriteLine($"MassTransit health check: {massTransitHealthCheck.Value.Status}");
    }

    [Fact]
    public async Task Should_Verify_Worker_Service_Health_Check()
    {
        // This test verifies that the custom worker health check is functioning

        // Arrange
        using var scope = _factory!.Services.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);

        var workerHealthCheck = result.Entries.FirstOrDefault(e => 
            e.Key.ToLower().Contains("worker"));

        workerHealthCheck.Should().NotBeNull();
        workerHealthCheck.Value.Status.Should().Be(HealthStatus.Healthy);

        // Worker health check should include data about services
        if (workerHealthCheck.Value.Data.Count > 0)
        {
            _output.WriteLine("Worker health check data:");
            foreach (var data in workerHealthCheck.Value.Data)
            {
                _output.WriteLine($"  {data.Key}: {data.Value}");
            }
        }
    }    
}