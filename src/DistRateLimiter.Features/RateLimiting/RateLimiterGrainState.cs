namespace DistRateLimiter.Features.RateLimiting;

[GenerateSerializer]
public class RateLimiterGrainState
{
    [Id(0)]
    public uint MaxTokens { get; set; }
    
    [Id(1)]
    public bool IsActive { get; set; }

    [Id(2)]
    public uint RemainingTokens { get; set; }

    [Id(3)]
    public DateTimeOffset LastRefillTime { get; set; }

    [Id(4)]
    public Dictionary<string, uint> ClientAcquireCollection { get; set; } = [];
}
