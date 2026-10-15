using DistRateLimiter.Core.Features.RateLimiting;
using Orleans.TestingHost;

namespace DistRateLimiter.Grain.Tests;

[Collection(ClusterCollection.Name)]
public class RateLimiterGrainTests
{
    private readonly TestCluster _cluster;

    public RateLimiterGrainTests(ClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task SetupAsync_WhenCalledFirstTime_ShouldSucceedAndInitializeState()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-1-{Guid.NewGuid()}");

        // Act
        var result = await grain.SetupAsync(100);
        var remaining = await grain.GetRemainingAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(100u, remaining);
    }

    [Fact]
    public async Task SetupAsync_WhenCalledSecondTime_ShouldReturnFailure()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-2-{Guid.NewGuid()}");
        await grain.SetupAsync(100);

        // Act
        var secondSetup = await grain.SetupAsync(200);

        // Assert
        Assert.False(secondSetup.IsSuccess);
        Assert.Contains("already set up", secondSetup.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcquireAsync_WithValidRequest_ShouldDeductTokensAndReturnSuccess()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-3-{Guid.NewGuid()}");
        await grain.SetupAsync(100);

        // Act
        var result = await grain.AcquireAsync("req-1", 30);
        var remaining = await grain.GetRemainingAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("req-1", result.Value.RequestId);
        Assert.Equal(30u, result.Value.GivenTokens);
        Assert.Equal(70u, result.Value.RemainingTokens);
        Assert.Equal(70u, remaining);
    }

    [Fact]
    public async Task AcquireAsync_WithDuplicateRequestId_ShouldReturnFailure()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-4-{Guid.NewGuid()}");
        await grain.SetupAsync(100);
        await grain.AcquireAsync("req-dup", 20);

        // Act
        var duplicateResult = await grain.AcquireAsync("req-dup", 10);

        // Assert
        Assert.False(duplicateResult.IsSuccess);
        Assert.Contains("already exists", duplicateResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcquireAsync_WhenRequestExceedsRemainingTokens_ShouldReturnFailure()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-5-{Guid.NewGuid()}");
        await grain.SetupAsync(50);

        // Act
        var result = await grain.AcquireAsync("req-over", 60);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Not enough tokens", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReturnUnusedTokensAsync_WithValidUnusedTokens_ShouldRestoreTokensAndRemoveRequestId()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-6-{Guid.NewGuid()}");
        await grain.SetupAsync(100);
        await grain.AcquireAsync("req-return", 40); // 60 remaining

        // Act
        var returnResult = await grain.ReturnUnusedTokensAsync("req-return", 15); // should restore 15 -> 75 remaining
        var remaining = await grain.GetRemainingAsync();

        // Assert
        Assert.True(returnResult.IsSuccess);
        Assert.Equal(75u, returnResult.Value.RemainingTokens);
        Assert.Equal(75u, remaining);
    }

    [Fact]
    public async Task ReturnUnusedTokensAsync_WithNonExistentRequestId_ShouldReturnFailure()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-7-{Guid.NewGuid()}");
        await grain.SetupAsync(100);

        // Act
        var result = await grain.ReturnUnusedTokensAsync("non-existent-id", 10);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReturnUnusedTokensAsync_WhenUnusedTokensExceedAcquiredAmount_ShouldReturnFailure()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-8-{Guid.NewGuid()}");
        await grain.SetupAsync(100);
        await grain.AcquireAsync("req-exceed-return", 20);

        // Act
        var result = await grain.ReturnUnusedTokensAsync("req-exceed-return", 25); // returning 25 when only 20 acquired

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("exceed acquired tokens", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcquireAsync_AfterReturningTokens_ShouldAllowReacquiringWithSameRequestId()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-9-{Guid.NewGuid()}");
        await grain.SetupAsync(100);
        await grain.AcquireAsync("reuse-req-id", 20);
        await grain.ReturnUnusedTokensAsync("reuse-req-id", 5); // removes request ID from collection

        // Act
        var reacquireResult = await grain.AcquireAsync("reuse-req-id", 10);

        // Assert
        Assert.True(reacquireResult.IsSuccess);
    }

    [Fact]
    public async Task GetRemainingAsync_ShouldReturnCurrentAvailableTokens()
    {
        // Arrange
        var grain = _cluster.GrainFactory.GetGrain<IRateLimiterGrain>($"tenant-10-{Guid.NewGuid()}");
        await grain.SetupAsync(80);

        // Act
        var remaining = await grain.GetRemainingAsync();

        // Assert
        Assert.Equal(80u, remaining);
    }
}
