namespace DistRateLimiter.WebApiApp.Models;

public record ReturnUnusedTokensArgsRecord(
    string ClientId,
    uint TokenAmount);

public record ReturnUnusedTokensResultRecord(
    bool Status,
    uint ReturnedAmount,
    string Message);
