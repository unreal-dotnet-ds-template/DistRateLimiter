namespace DistRateLimiter.WebApiApp.Models;

public record SetupArgRecord(
    string KeyId,
    uint MaxTokens);

public record SetupResultRecord(
    string KeyId,
    bool Status);
