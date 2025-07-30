namespace WorkerService.Worker.Configuration;

/// <summary>
/// Configuration settings for JWT token authentication
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";
    
    /// <summary>
    /// Secret key used for JWT token signing and validation
    /// </summary>
    public string SecretKey { get; set; } = "your-256-bit-secret-key-here-make-it-long-enough-for-hmac-sha256-algorithm";
    
    /// <summary>
    /// Token issuer (who created the token)
    /// </summary>
    public string Issuer { get; set; } = "WorkerService.API";
    
    /// <summary>
    /// Token audience (who the token is intended for)
    /// </summary>
    public string Audience { get; set; } = "WorkerService.Client";
    
    /// <summary>
    /// Token expiration time in minutes
    /// </summary>
    public int ExpireMinutes { get; set; } = 60;
}