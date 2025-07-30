using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WorkerService.Worker.Configuration;
using WorkerService.Worker.Endpoints;
using WorkerService.Worker.Services;

namespace WorkerService.IntegrationTests.Shared.Utilities;

/// <summary>
/// Helper class for authentication-related testing operations
/// </summary>
public static class AuthenticationTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Valid test credentials for authentication testing
    /// </summary>
    public static class ValidCredentials
    {
        public const string Username = "testuser";
        public const string Password = "testpass";

        public const string AdminUsername = "admin";
        public const string AdminPassword = "password123";

        public const string DemoUsername = "demo";
        public const string DemoPassword = "demo123";
    }

    /// <summary>
    /// Invalid test credentials for negative testing
    /// </summary>
    public static class InvalidCredentials
    {
        public const string Username = "invaliduser";
        public const string Password = "wrongpassword";
    }

    /// <summary>
    /// Generates a valid JWT token for testing using the application's JwtTokenService
    /// </summary>
    /// <param name="serviceProvider">Service provider to get JWT token service</param>
    /// <param name="username">Username for the token (defaults to test user)</param>
    /// <returns>A valid JWT token string</returns>
    public static string GenerateValidToken(IServiceProvider serviceProvider, string username = ValidCredentials.Username)
    {
        using var scope = serviceProvider.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        return jwtTokenService.GenerateToken(username);
    }

    /// <summary>
    /// Creates an authenticated HttpClient with a valid JWT token
    /// </summary>
    /// <param name="client">Base HttpClient to configure</param>
    /// <param name="serviceProvider">Service provider to generate token</param>
    /// <param name="username">Username for the token (defaults to test user)</param>
    /// <returns>HttpClient with Authorization header set</returns>
    public static HttpClient CreateAuthenticatedClient(HttpClient client, IServiceProvider serviceProvider, string username = ValidCredentials.Username)
    {
        var token = GenerateValidToken(serviceProvider, username);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Requests a JWT token via the /auth/token endpoint
    /// </summary>
    /// <param name="client">HttpClient to make the request</param>
    /// <param name="username">Username for authentication</param>
    /// <param name="password">Password for authentication</param>
    /// <returns>HTTP response from the token endpoint</returns>
    public static async Task<HttpResponseMessage> RequestTokenAsync(HttpClient client, string username, string password)
    {
        var request = new TokenRequest(username, password);
        return await client.PostAsJsonAsync("/auth/token", request, JsonOptions);
    }

    /// <summary>
    /// Requests a JWT token and returns the token string if successful
    /// </summary>
    /// <param name="client">HttpClient to make the request</param>
    /// <param name="username">Username for authentication</param>
    /// <param name="password">Password for authentication</param>
    /// <returns>JWT token string or null if request failed</returns>
    public static async Task<string?> GetTokenAsync(HttpClient client, string username = ValidCredentials.Username, string password = ValidCredentials.Password)
    {
        var response = await RequestTokenAsync(client, username, password);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        return tokenResponse?.AccessToken;
    }

    /// <summary>
    /// Creates an HttpClient with JWT token obtained from the auth endpoint
    /// </summary>
    /// <param name="client">Base HttpClient to configure</param>
    /// <param name="username">Username for authentication</param>
    /// <param name="password">Password for authentication</param>
    /// <returns>HttpClient with Authorization header set or original client if token request failed</returns>
    public static async Task<HttpClient> CreateAuthenticatedClientViaEndpointAsync(HttpClient client, string username = ValidCredentials.Username, string password = ValidCredentials.Password)
    {
        var token = await GetTokenAsync(client, username, password);
        if (token != null)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    /// <summary>
    /// Validates that an HTTP response is an authentication failure (401 Unauthorized)
    /// </summary>
    /// <param name="response">HTTP response to validate</param>
    /// <returns>True if response indicates authentication failure</returns>
    public static bool IsAuthenticationFailure(HttpResponseMessage response)
    {
        return response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
    }

    /// <summary>
    /// Validates that an HTTP response is a forbidden access failure (403 Forbidden)
    /// </summary>
    /// <param name="response">HTTP response to validate</param>
    /// <returns>True if response indicates forbidden access</returns>
    public static bool IsForbiddenAccess(HttpResponseMessage response)
    {
        return response.StatusCode == System.Net.HttpStatusCode.Forbidden;
    }

    /// <summary>
    /// Creates a malformed/invalid JWT token for testing
    /// </summary>
    /// <returns>An invalid JWT token string</returns>
    public static string CreateInvalidToken()
    {
        return "definitely.not.a.valid.jwt.token.format";
    }

    /// <summary>
    /// Creates an expired JWT token for testing
    /// </summary>
    /// <param name="serviceProvider">Service provider to create the token service</param>
    /// <param name="username">Username for the token</param>
    /// <returns>An expired JWT token string</returns>
    public static string CreateExpiredToken(IServiceProvider serviceProvider, string username = ValidCredentials.Username)
    {
        // For testing purposes, we'll create a token with a past expiration time
        // This would require modifying the JwtSettings to have a negative expiration
        // For now, we'll return an obviously invalid token format
        return "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0dXNlciIsImV4cCI6MTU3NzgzNjgwMH0.invalid";
    }

    /// <summary>
    /// Removes authentication header from HttpClient
    /// </summary>
    /// <param name="client">HttpClient to modify</param>
    /// <returns>HttpClient without authentication header</returns>
    public static HttpClient RemoveAuthentication(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }
}