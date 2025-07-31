using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorkerService.IntegrationTests.InMemory.Fixtures;
using WorkerService.IntegrationTests.Shared.Utilities;
using WorkerService.Worker.Endpoints;
using Xunit;

namespace WorkerService.IntegrationTests.InMemory.Tests;

[Collection("InMemory Integration Tests")]
public class AuthenticationInMemoryTests : IClassFixture<InMemoryWebApplicationFactory>, IAsyncDisposable
{
    private readonly InMemoryWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthenticationInMemoryTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region POST /auth/token Tests

    [Fact]
    public async Task PostAuthToken_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        var request = new TokenRequest(
            AuthenticationTestHelper.ValidCredentials.Username, 
            AuthenticationTestHelper.ValidCredentials.Password);

        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(_jsonOptions);
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.TokenType.Should().Be("Bearer");

        // Verify the token is a valid JWT
        var handler = new JwtSecurityTokenHandler();
        var isValidJwt = handler.CanReadToken(result.AccessToken);
        isValidJwt.Should().BeTrue();
    }

    [Theory]
    [InlineData("admin", "password123")]
    [InlineData("testuser", "testpass")]
    [InlineData("demo", "demo123")]
    public async Task PostAuthToken_WithDifferentValidCredentials_ShouldReturnJwtToken(string username, string password)
    {
        // Arrange
        var request = new TokenRequest(username, password);

        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(_jsonOptions);
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task PostAuthToken_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new TokenRequest(
            AuthenticationTestHelper.InvalidCredentials.Username, 
            AuthenticationTestHelper.InvalidCredentials.Password);

        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("validuser", "wrongpassword")]
    [InlineData("admin", "wrongpassword")]
    [InlineData("nonexistentuser", "password")]
    public async Task PostAuthToken_WithDifferentInvalidCredentials_ShouldReturnUnauthorized(string username, string password)
    {
        // Arrange
        var request = new TokenRequest(username, password);

        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostAuthToken_WithMissingCredentials_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new TokenRequest("", "");

        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Message.Should().Contain("Username and password are required");
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("username", "")]
    [InlineData("   ", "password")]
    [InlineData("username", "   ")]
    public async Task PostAuthToken_WithMissingUsernameOrPassword_ShouldReturnBadRequest(string username, string password)
    {
        // Arrange
        var request = new TokenRequest(username, password);

        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAuthToken_WithNullRequest_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", (TokenRequest?)null, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GeneratedToken_ShouldBeValidJwt()
    {
        // Arrange
        var request = new TokenRequest(
            AuthenticationTestHelper.ValidCredentials.Username, 
            AuthenticationTestHelper.ValidCredentials.Password);

        // Act
        var response = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(_jsonOptions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(result!.AccessToken);
        
        jsonToken.Should().NotBeNull();
        jsonToken.Claims.Should().Contain(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" && 
                                               c.Value == AuthenticationTestHelper.ValidCredentials.Username);
        jsonToken.Claims.Should().Contain(c => c.Type == "sub" && 
                                               c.Value == AuthenticationTestHelper.ValidCredentials.Username);
        jsonToken.Claims.Should().Contain(c => c.Type == "jti");
        jsonToken.Claims.Should().Contain(c => c.Type == "iat");
        
        jsonToken.Issuer.Should().Be("WorkerService.API");
        jsonToken.Audiences.Should().Contain("WorkerService.Client");
        
        // Verify token is not expired
        jsonToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task PostAuthToken_MultipleCalls_ShouldGenerateUniqueTokens()
    {
        // Arrange
        var request = new TokenRequest(
            AuthenticationTestHelper.ValidCredentials.Username, 
            AuthenticationTestHelper.ValidCredentials.Password);

        // Act
        var response1 = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);
        var response2 = await _client.PostAsJsonAsync("/auth/token", request, _jsonOptions);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var result1 = await response1.Content.ReadFromJsonAsync<TokenResponse>(_jsonOptions);
        var result2 = await response2.Content.ReadFromJsonAsync<TokenResponse>(_jsonOptions);

        result1!.AccessToken.Should().NotBe(result2!.AccessToken);
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void AuthenticationTestHelper_ValidCredentials_ShouldBeConfiguredCorrectly()
    {
        // Assert
        AuthenticationTestHelper.ValidCredentials.Username.Should().NotBeNullOrEmpty();
        AuthenticationTestHelper.ValidCredentials.Password.Should().NotBeNullOrEmpty();
        AuthenticationTestHelper.ValidCredentials.AdminUsername.Should().NotBeNullOrEmpty();
        AuthenticationTestHelper.ValidCredentials.AdminPassword.Should().NotBeNullOrEmpty();
        AuthenticationTestHelper.ValidCredentials.DemoUsername.Should().NotBeNullOrEmpty();
        AuthenticationTestHelper.ValidCredentials.DemoPassword.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthenticationTestHelper_GetTokenAsync_WithValidCredentials_ShouldReturnToken()
    {
        // Act
        var token = await AuthenticationTestHelper.GetTokenAsync(_client);

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticationTestHelper_GetTokenAsync_WithInvalidCredentials_ShouldReturnNull()
    {
        // Act
        var token = await AuthenticationTestHelper.GetTokenAsync(_client, 
            AuthenticationTestHelper.InvalidCredentials.Username, 
            AuthenticationTestHelper.InvalidCredentials.Password);

        // Assert
        token.Should().BeNull();
    }

    [Fact]
    public void AuthenticationTestHelper_GenerateValidToken_ShouldCreateValidToken()
    {
        // Act
        var token = AuthenticationTestHelper.GenerateValidToken(_factory.Services);

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
        
        var jsonToken = handler.ReadJwtToken(token);
        jsonToken.Claims.Should().Contain(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" && 
                                               c.Value == AuthenticationTestHelper.ValidCredentials.Username);
    }

    [Fact]
    public void AuthenticationTestHelper_CreateInvalidToken_ShouldReturnInvalidToken()
    {
        // Act
        var token = AuthenticationTestHelper.CreateInvalidToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeFalse();
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _factory.ClearDatabaseAsync();
        _client.Dispose();
    }
}