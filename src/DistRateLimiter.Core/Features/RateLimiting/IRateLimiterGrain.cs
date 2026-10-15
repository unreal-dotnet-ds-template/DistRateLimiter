namespace DistRateLimiter.Core.Features.RateLimiting;

[Alias("DistRateLimiter.Core.Features.RateLimiting.IRateLimiterGrain")]
public interface IRateLimiterGrain : IGrainWithStringKey
{
    [Alias("DistRateLimiter.Core.Features.RateLimiting.SetupAsync")]
    [Id(0)]
    ValueTask<GrainResult<bool>> SetupAsync(
        [Id(0)] uint maxTokens);

    [Alias("DistRateLimiter.Core.Features.RateLimiting.AcquireAsync")]
    [Id(1)]
    ValueTask<GrainResult<TokenAcquireResult>> AcquireAsync(
        [Id(0)] string requestId,
        [Id(1)] uint requestAmount);

    [Alias("DistRateLimiter.Core.Features.RateLimiting.ReturnUnusedTokensAsync")]
    [Id(2)]
    ValueTask<GrainResult<ReturnUnusedTokensResult>> ReturnUnusedTokensAsync(
        [Id(0)] string requestId,
        [Id(1)] uint unusedTokens);

    [Alias("DistRateLimiter.Core.Features.RateLimiting.GetRemainingAsync")]
    [Id(3)]
    ValueTask<uint> GetRemainingAsync();
}

[Alias("DistRateLimiter.Core.Features.RateLimiting.TokenAcquireResult")]
[GenerateSerializer]
public record TokenAcquireResult(
    [Id(0)]
    string RequestId,
    [Id(1)]
    uint GivenTokens,
    [Id(2)]
    uint RemainingTokens);

[Alias("DistRateLimiter.Core.Features.RateLimiting.ReturnUnusedTokensResult")]
[GenerateSerializer]
public record ReturnUnusedTokensResult(
    [Id(0)]
    uint RemainingTokens);
