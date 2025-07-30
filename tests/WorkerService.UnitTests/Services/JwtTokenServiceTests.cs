using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WorkerService.Worker.Configuration;
using WorkerService.Worker.Services;
using Xunit;

namespace WorkerService.UnitTests.Services;

public class JwtTokenServiceTests
{
    private readonly Mock<ILogger<JwtTokenService>> _mockLogger;
    private readonly JwtSettings _jwtSettings;
    private readonly IOptions<JwtSettings> _mockOptions;
    private readonly JwtTokenService _service;

    public JwtTokenServiceTests()
    {
        _mockLogger = new Mock<ILogger<JwtTokenService>>();
        _jwtSettings = new JwtSettings
        {
            SecretKey = "super-secret-key-that-is-long-enough-for-hmac-sha256-algorithm-testing",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpireMinutes = 60
        };
        _mockOptions = Options.Create(_jwtSettings);
        _service = new JwtTokenService(_mockOptions, _mockLogger.Object);
    }

    [Fact]
    public void GenerateToken_WithValidUsername_ShouldReturnValidJwt()
    {
        // Arrange
        var username = "testuser";

        // Act
        var token = _service.GenerateToken(username);

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid JWT format (3 parts separated by dots)
        var parts = token.Split('.');
        parts.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void GenerateToken_WithEmptyUsername_ShouldThrowException(string username)
    {
        // Act & Assert
        var action = () => _service.GenerateToken(username);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Username cannot be null or empty*");
    }

    [Fact]
    public void ValidateTokenStructure_ShouldContainExpectedClaims()
    {
        // Arrange
        var username = "testuser";
        var token = _service.GenerateToken(username);

        // Act
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        // Assert
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == username);
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == username);
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Iat);
        
        jsonToken.Issuer.Should().Be(_jwtSettings.Issuer);
        jsonToken.Audiences.Should().Contain(_jwtSettings.Audience);
    }

    [Fact]
    public void TokenExpiration_ShouldMatchConfiguration()
    {
        // Arrange
        var username = "testuser";
        var beforeGeneration = DateTime.UtcNow.AddSeconds(-1); // Add small buffer for timing
        
        // Act
        var token = _service.GenerateToken(username);
        var afterGeneration = DateTime.UtcNow.AddSeconds(1); // Add small buffer for timing

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        
        var expectedMinExpiry = beforeGeneration.AddMinutes(_jwtSettings.ExpireMinutes);
        var expectedMaxExpiry = afterGeneration.AddMinutes(_jwtSettings.ExpireMinutes);
        
        jsonToken.ValidTo.Should().BeOnOrAfter(expectedMinExpiry);
        jsonToken.ValidTo.Should().BeOnOrBefore(expectedMaxExpiry);
    }

    [Fact]
    public void GenerateToken_MultipleCalls_ShouldGenerateUniqueTokens()
    {
        // Arrange
        var username = "testuser";

        // Act
        var token1 = _service.GenerateToken(username);
        var token2 = _service.GenerateToken(username);

        // Assert
        token1.Should().NotBe(token2);
        
        // Verify both tokens have different JTI (unique identifier)
        var handler = new JwtSecurityTokenHandler();
        var jsonToken1 = handler.ReadJwtToken(token1);
        var jsonToken2 = handler.ReadJwtToken(token2);
        
        var jti1 = jsonToken1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jsonToken2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        
        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void GenerateToken_WithSpecialCharactersInUsername_ShouldHandleCorrectly()
    {
        // Arrange
        var username = "test.user+special@domain.com";

        // Act
        var token = _service.GenerateToken(username);

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == username);
    }
}