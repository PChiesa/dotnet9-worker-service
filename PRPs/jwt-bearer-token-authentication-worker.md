# PRP-JWT-Bearer-Token-Authentication.md

## Feature Title
JWT Bearer Token Authentication for Orders API

## Technical Overview
Implement JWT Bearer Token authentication to protect all `/api/orders` endpoints from anonymous access. The solution will provide a simple token generation endpoint (`/auth/token`) and protect existing Orders API endpoints using JWT authentication middleware. Authentication logic will be contained exclusively in the Worker layer to maintain Clean Architecture compliance, with hardcoded JWT configuration for simplicity.

The implementation leverages .NET 9's built-in JWT authentication middleware and will integrate seamlessly with the existing Orders Controller while maintaining the current API contracts. Token validation will be signature-based only, with username claims for user identification.

## Effort Score
**6/10** - Moderate complexity due to JWT implementation, middleware configuration, and comprehensive testing requirements, but simplified by leveraging built-in .NET authentication.

## Success Chance Score
**9/10** - High confidence. JWT authentication is a well-established pattern in .NET 9, and the requirements are straightforward with clear boundaries.

## Implementation Steps

### Step 1: Add JWT Authentication Dependencies
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\WorkerService.Worker.csproj`

**Actions:**
- Add `Microsoft.AspNetCore.Authentication.JwtBearer` package reference
- Add `System.IdentityModel.Tokens.Jwt` package reference

### Step 2: Create JWT Configuration Settings
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Configuration\JwtSettings.cs`

**Actions:**
- Create strongly-typed configuration class for JWT settings
- Include properties for SecretKey, Issuer, Audience, ExpireMinutes
- Follow existing configuration pattern used by other settings classes

### Step 3: Create JWT Token Generation Service
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Services\JwtTokenService.cs`

**Actions:**
- Create service to generate JWT tokens with username claims
- Implement token creation with hardcoded configuration
- Follow existing service patterns and dependency injection registration

### Step 4: Create Authentication Endpoints
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Endpoints\AuthEndpoints.cs`

**Actions:**
- Create minimal API endpoint for `/auth/token` POST request
- Implement simple credential validation (hardcoded for simplicity)
- Return JWT token in response with appropriate status codes
- Follow existing endpoint patterns used in ItemEndpoints.cs

### Step 5: Configure JWT Authentication in Program.cs
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Program.cs`

**Actions:**
- Add JWT authentication configuration with hardcoded settings
- Configure JWT bearer options (issuer, audience, signing key)
- Add authentication and authorization middleware to pipeline
- Register JwtTokenService in DI container
- Map AuthEndpoints to application

### Step 6: Protect Orders Controller with Authentication
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Controllers\OrdersController.cs`

**Actions:**
- Add `[Authorize]` attribute to OrdersController class
- Add required using statements for authorization
- Ensure all existing endpoints maintain current functionality for authenticated users

### Step 7: Update OpenAPI Documentation
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\Program.cs`

**Actions:**
- Update Swagger configuration to include JWT bearer authentication schema
- Add security definitions for JWT tokens in OpenAPI documentation
- Configure Swagger UI to show authentication option

### Step 8: Add JWT Configuration to appsettings
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\appsettings.json`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\appsettings.Development.json`
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\src\WorkerService.Worker\appsettings.Test.json`

**Actions:**
- Add JWT configuration section with hardcoded values
- Include different configurations for each environment
- Ensure test configuration supports integration testing

## Testing Plan

### Unit Tests
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.UnitTests\Services\JwtTokenServiceTests.cs`

**Test scenarios:**
- `GenerateToken_WithValidUsername_ShouldReturnValidJwt()`
- `GenerateToken_WithEmptyUsername_ShouldThrowException()`
- `ValidateTokenStructure_ShouldContainExpectedClaims()`
- `TokenExpiration_ShouldMatchConfiguration()`

### Integration Tests - Authentication Endpoints
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.IntegrationTests\InMemory\Tests\AuthenticationInMemoryTests.cs`

**Test scenarios:**
- `PostAuthToken_WithValidCredentials_ShouldReturnJwtToken()`
- `PostAuthToken_WithInvalidCredentials_ShouldReturnUnauthorized()`
- `PostAuthToken_WithMissingCredentials_ShouldReturnBadRequest()`
- `GeneratedToken_ShouldBeValidJwt()`

### Integration Tests - Protected Orders API
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.IntegrationTests\InMemory\Tests\OrdersApiInMemoryTests.cs`

**Test scenarios to add:**
- `OrdersEndpoints_WithoutToken_ShouldReturnUnauthorized()`
- `OrdersEndpoints_WithValidToken_ShouldReturnSuccess()`
- `OrdersEndpoints_WithExpiredToken_ShouldReturnUnauthorized()`
- `OrdersEndpoints_WithInvalidToken_ShouldReturnUnauthorized()`

**Existing tests to modify:**
- Update all existing test methods to include valid JWT token in Authorization header
- Create helper method `GetAuthenticatedClient()` to provide pre-authenticated HttpClient
- Ensure all existing functionality tests pass with authentication enabled

### Integration Tests - Container Testing
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.IntegrationTests\Container\Tests\OrderProcessingContainerTests.cs`

**Actions:**
- Add authentication token generation to container tests
- Ensure background processing works with authentication enabled
- Test JWT token validation in container environment

### Test Fixtures Updates
**Files to modify:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.IntegrationTests\InMemory\Fixtures\InMemoryWebApplicationFactory.cs`

**Actions:**
- Add JWT configuration to test application factory
- Provide helper methods for generating test tokens
- Ensure authentication is properly configured for integration tests

### Test Utilities
**Files to create:**
- `C:\Users\pedro.simoes\source\repos\dotnet9-worker-service\tests\WorkerService.IntegrationTests\Shared\Utilities\AuthenticationTestHelper.cs`

**Actions:**
- Create utility class for generating test JWT tokens
- Provide methods for creating authenticated HTTP clients
- Include token validation helpers for test assertions

## Implementation Details

### JWT Configuration Structure
```csharp
public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string SecretKey { get; set; } = "your-256-bit-secret-key-here-make-it-long-enough";
    public string Issuer { get; set; } = "WorkerService.API";
    public string Audience { get; set; } = "WorkerService.Client";
    public int ExpireMinutes { get; set; } = 60;
}
```

### Authentication Flow
1. Client requests token via `POST /auth/token` with credentials
2. Server validates credentials (hardcoded validation)
3. Server generates JWT token with username claim
4. Client includes token in `Authorization: Bearer <token>` header for Orders API calls
5. JWT middleware validates token signature and extracts claims
6. Orders Controller processes request if token is valid, returns 403 if invalid

### Security Considerations
- JWT secret key will be hardcoded but sufficiently complex for development
- Token expiration set to 60 minutes for reasonable session length
- Only signature validation performed (no audience/issuer validation for simplicity)
- HTTPS enforcement recommended for production deployment (outside scope)

### Backward Compatibility
- ItemEndpoints remain unprotected as per requirements
- Health check endpoints remain accessible without authentication
- All existing Orders API functionality preserved for authenticated users
- OpenAPI/Swagger documentation updated to reflect authentication requirements

This implementation provides a secure, testable, and maintainable JWT authentication solution that adheres to Clean Architecture principles while meeting all specified requirements.