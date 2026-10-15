namespace DistRateLimiter.WebApiApp.Models;

public record AcquireLeaseArgRecord(
    string ClientId,
    uint RequestAmount = 50);

public record AcquireLeaseResultRecord(
    bool IsAcquired,
    uint ProvidedAmount,
    uint RemainingAmount,
    string? Message);
