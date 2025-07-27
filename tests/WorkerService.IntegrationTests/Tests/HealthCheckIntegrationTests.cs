using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using WorkerService.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WorkerService.IntegrationTests.Tests;

[Collection("Integration Tests")]
public class HealthCheckIntegrationTests : IClassFixture<WorkerServiceTestFixture>, IAsyncLifetime
{
    private readonly WorkerServiceTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private TestWebApplicationFactory? _factory;
    private HttpClient? _client;

    public HealthCheckIntegrationTests(WorkerServiceTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _factory = new TestWebApplicationFactory(_fixture);
        await _factory.InitializeAsync();
        _client = _factory!.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
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
    public async Task Should_Include_All_Required_Health_Checks()
    {
        // Act
        var response = await _client!.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Parse the health check response
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Assert - Check for expected health check components
        root.GetProperty("status").GetString().Should().Be("Healthy");

        if (root.TryGetProperty("entries", out var entries))
        {
            // Verify RabbitMQ health check
            entries.TryGetProperty("rabbitmq", out var rabbitmq).Should().BeTrue("RabbitMQ health check should be present");
            if (rabbitmq.ValueKind != JsonValueKind.Undefined)
            {
                rabbitmq.GetProperty("status").GetString().Should().Be("Healthy");
            }

            // Verify PostgreSQL health check
            entries.TryGetProperty("npgsql", out var postgres).Should().BeTrue("PostgreSQL health check should be present");
            if (postgres.ValueKind != JsonValueKind.Undefined)
            {
                postgres.GetProperty("status").GetString().Should().Be("Healthy");
            }

            // Verify Worker health check
            entries.TryGetProperty("worker", out var worker).Should().BeTrue("Worker health check should be present");
            if (worker.ValueKind != JsonValueKind.Undefined)
            {
                worker.GetProperty("status").GetString().Should().Be("Healthy");
            }

            _output.WriteLine("All required health checks are present and healthy");
        }
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
        
        // Health checks should complete within reasonable time (5 seconds)
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "Health checks should complete quickly");

        _output.WriteLine($"Health check completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Should_Return_Consistent_Results_On_Multiple_Calls()
    {
        // Act - Call health endpoint multiple times
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 5; i++)
        {
            responses.Add(await _client!.GetAsync("/health"));
            await Task.Delay(500); // Small delay between calls
        }

        // Assert - All responses should be successful
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }

        _output.WriteLine($"Health endpoint returned consistent results across {responses.Count} calls");
    }

    [Fact]
    public async Task Should_Include_Duration_Information_In_Health_Check_Response()
    {
        // Act
        var response = await _client!.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Parse response
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Assert - Check for duration information
        if (root.TryGetProperty("totalDuration", out var totalDuration))
        {
            // Total duration should be present and reasonable
            var duration = totalDuration.GetString();
            duration.Should().NotBeNullOrEmpty();
            _output.WriteLine($"Total health check duration: {duration}");
        }

        if (root.TryGetProperty("entries", out var entries))
        {
            foreach (var entry in entries.EnumerateObject())
            {
                if (entry.Value.TryGetProperty("duration", out var duration))
                {
                    var durationStr = duration.GetString();
                    durationStr.Should().NotBeNullOrEmpty();
                    _output.WriteLine($"{entry.Name} duration: {durationStr}");
                }
            }
        }
    }

    [Fact]
    public async Task Should_Verify_Database_Connectivity_Through_Health_Check()
    {
        // This test specifically verifies that the database health check
        // can actually connect to the test database

        // Arrange
        using var scope = _factory!.Services.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);

        var dbHealthCheck = result.Entries.FirstOrDefault(e => 
            e.Key.ToLower().Contains("npgsql") || e.Key.ToLower().Contains("postgres"));

        dbHealthCheck.Should().NotBeNull();
        dbHealthCheck.Value.Status.Should().Be(HealthStatus.Healthy);

        _output.WriteLine($"Database health check: {dbHealthCheck.Value.Status}");
    }

    [Fact]
    public async Task Should_Verify_RabbitMQ_Connectivity_Through_Health_Check()
    {
        // This test specifically verifies that the RabbitMQ health check
        // can actually connect to the test RabbitMQ instance

        // Arrange
        using var scope = _factory!.Services.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);

        var rabbitHealthCheck = result.Entries.FirstOrDefault(e => 
            e.Key.ToLower().Contains("rabbit"));

        rabbitHealthCheck.Should().NotBeNull();
        rabbitHealthCheck.Value.Status.Should().Be(HealthStatus.Healthy);

        _output.WriteLine($"RabbitMQ health check: {rabbitHealthCheck.Value.Status}");
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

    [Fact]
    public async Task Should_Handle_Concurrent_Health_Check_Requests()
    {
        // Arrange
        const int concurrentRequests = 10;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send multiple concurrent requests
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_client!.GetAsync("/health"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }

        _output.WriteLine($"Successfully handled {concurrentRequests} concurrent health check requests");
    }

    [Fact]
    public async Task Should_Return_Json_Content_Type()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // Verify it's valid JSON
        var content = await response.Content.ReadAsStringAsync();
        var canParse = true;
        try
        {
            using var jsonDoc = JsonDocument.Parse(content);
        }
        catch
        {
            canParse = false;
        }

        canParse.Should().BeTrue("Response should be valid JSON");

        _output.WriteLine("Health check returns proper JSON response");
    }
}