using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WorkerService.Worker.Services;

namespace WorkerService.Worker.Endpoints;

/// <summary>
/// Authentication endpoints for JWT token management
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication endpoints to the route group
    /// </summary>
    /// <param name="group">The route group builder</param>
    /// <returns>The configured route group</returns>
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        // POST /auth/token
        group.MapPost("/token", GenerateToken)
            .WithName("GenerateToken")
            .WithSummary("Generate JWT token")
            .WithDescription("Generates a JWT token for authenticated access to protected endpoints")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    /// <summary>
    /// Generates a JWT token based on provided credentials
    /// </summary>
    /// <param name="request">The token request containing credentials</param>
    /// <param name="jwtTokenService">The JWT token service</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A JWT token response or error</returns>
    private static Results<Ok<TokenResponse>, BadRequest<ErrorResponse>, UnauthorizedHttpResult> GenerateToken(
        [FromBody] TokenRequest request,
        JwtTokenService jwtTokenService,
        ILogger<JwtTokenService> logger)
    {
        try
        {
            // Validate request
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                logger.LogWarning("Token generation attempted with missing credentials");
                return TypedResults.BadRequest(new ErrorResponse("Username and password are required"));
            }

            // Simple hardcoded credential validation (as per requirements)
            if (!IsValidCredentials(request.Username, request.Password))
            {
                logger.LogWarning("Token generation failed for username {Username} - invalid credentials", request.Username);
                return TypedResults.Unauthorized();
            }

            // Generate JWT token
            var token = jwtTokenService.GenerateToken(request.Username);

            logger.LogInformation("JWT token generated successfully for user {Username}", request.Username);

            return TypedResults.Ok(new TokenResponse(token, "Bearer"));
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid argument during token generation");
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during token generation");
            return TypedResults.BadRequest(new ErrorResponse("An error occurred while generating the token"));
        }
    }

    /// <summary>
    /// Validates user credentials (hardcoded for simplicity as per requirements)
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    /// <returns>True if credentials are valid, false otherwise</returns>
    private static bool IsValidCredentials(string username, string password)
    {
        // Hardcoded credentials for development/testing as per requirements
        var validCredentials = new Dictionary<string, string>
        {
            { "admin", "password123" },
            { "testuser", "testpass" },
            { "demo", "demo123" }
        };

        return validCredentials.TryGetValue(username, out var validPassword) && validPassword == password;
    }
}

/// <summary>
/// Request model for token generation
/// </summary>
/// <param name="Username">The username</param>
/// <param name="Password">The password</param>
public record TokenRequest(string Username, string Password);

/// <summary>
/// Response model for successful token generation
/// </summary>
/// <param name="AccessToken">The JWT access token</param>
/// <param name="TokenType">The token type (always "Bearer")</param>
public record TokenResponse(string AccessToken, string TokenType);

/// <summary>
/// Response model for error cases
/// </summary>
/// <param name="Message">The error message</param>
public record ErrorResponse(string Message);