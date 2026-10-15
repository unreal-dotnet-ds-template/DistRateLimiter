using DistRateLimiter.Core.Features;
using DistRateLimiter.Core.Features.RateLimiting;
using Orleans.Concurrency;

namespace DistRateLimiter.Features.RateLimiting;

/// <summary>
/// for Testing purposes we are assuming that:
/// MaxToken: 100
/// Refilling rate: 10 tokens per minute.
/// grainId: $"{tenantId}-{keyId}"
/// Example: "google-myappservice"
/// Example: "google-mysearch"
/// </summary>
public class RateLimiterGrain : Grain, IRateLimiterGrain
{
    private readonly IPersistentState<RateLimiterGrainState> _stateProvider;
    private IDisposable? _refillTimer;
    private uint _refillRatePerMinute = 10;
    private bool _isDirty;
    private IDisposable? _stateWriteTimer;

    public RateLimiterGrain(
        [PersistentState("requests", "tokensStore")]
        IPersistentState<RateLimiterGrainState> stateProvider)
    {
        _stateProvider = stateProvider;
    }

    public ValueTask<GrainResult<bool>> SetupAsync(uint maxTokens)
    {
        if (_stateProvider.State.IsActive) return ValueTask.FromResult(GrainResult<bool>.Failure("Rate limiter is already set up."));

        _stateProvider.State.IsActive = true;
        _stateProvider.State.MaxTokens = maxTokens;
        _stateProvider.State.LastRefillTime = DateTimeOffset.UtcNow;
        _stateProvider.State.RemainingTokens = maxTokens;
        _stateProvider.State.ClientAcquireCollection = new Dictionary<string, uint>();
        _isDirty = true;

        return ValueTask.FromResult(GrainResult<bool>.Success(true));
    }

    public ValueTask<GrainResult<TokenAcquireResult>> AcquireAsync(string requestId, uint requestAmount)
    {
        if(_stateProvider.State.IsActive is false)
            return ValueTask.FromResult(GrainResult<TokenAcquireResult>.Failure("Rate limiter is not set up yet."));

        EnsureOrStartRefellingTimer();

        if (_stateProvider.State.ClientAcquireCollection.ContainsKey(requestId))
            return ValueTask.FromResult(GrainResult<TokenAcquireResult>.Failure("Request ID already exists."));

        if (_stateProvider.State.RemainingTokens < requestAmount)
            return ValueTask.FromResult(GrainResult<TokenAcquireResult>.Failure("Not enough tokens available."));

        _stateProvider.State.RemainingTokens -= requestAmount;
        _stateProvider.State.ClientAcquireCollection.Add(requestId, requestAmount);
        _isDirty = true;

        return ValueTask.FromResult(
            GrainResult<TokenAcquireResult>.Success(new (requestId, requestAmount, _stateProvider.State.RemainingTokens)));
    }

    public ValueTask<GrainResult<ReturnUnusedTokensResult>> ReturnUnusedTokensAsync(string requestId, uint unusedTokens)
    {
        if (_stateProvider.State.IsActive is false)
            return ValueTask.FromResult(GrainResult<ReturnUnusedTokensResult>.Failure("Rate limiter is not set up yet."));

        EnsureOrStartRefellingTimer();

        if (_stateProvider.State.ClientAcquireCollection.ContainsKey(requestId) is false)
            return ValueTask.FromResult(GrainResult<ReturnUnusedTokensResult>.Failure("Request ID not found."));

        if(unusedTokens > _stateProvider.State.ClientAcquireCollection[requestId])
            return ValueTask.FromResult(GrainResult<ReturnUnusedTokensResult>.Failure("Unused tokens exceed acquired tokens."));

        _stateProvider.State.RemainingTokens = Math.Min(_stateProvider.State.MaxTokens, _stateProvider.State.RemainingTokens + unusedTokens);
        _stateProvider.State.ClientAcquireCollection.Remove(requestId);
        _isDirty = true;

        return ValueTask.FromResult(
            GrainResult<ReturnUnusedTokensResult>.Success(new (_stateProvider.State.RemainingTokens)));
    }

    [ReadOnly]
    public ValueTask<uint> GetRemainingAsync() => ValueTask.FromResult(_stateProvider.State.RemainingTokens);

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await _stateProvider.ReadStateAsync(cancellationToken).ConfigureAwait(true);

        EnsureOrStartRefellingTimer();
        EnsureOrStartStateWriteTimer();

        await base.OnActivateAsync(cancellationToken).ConfigureAwait(true);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _refillTimer?.Dispose();
        _refillTimer = null;
        _stateWriteTimer?.Dispose();
        _stateWriteTimer = null;

        await WriteStateIfDirtyAsync(cancellationToken).ConfigureAwait(true);
        await base.OnDeactivateAsync(reason, cancellationToken).ConfigureAwait(true);
    }

    private void EnsureOrStartRefellingTimer()
    {
        if (_refillTimer is not null) return;

        _refillTimer = this.RegisterGrainTimer(
            callback: TickRefillAsync,
            options: new(
                dueTime: TimeSpan.FromMilliseconds(200),
                period: TimeSpan.FromSeconds(30)));
    }

    private Task TickRefillAsync(CancellationToken token)
    {
        if (_stateProvider.State.RemainingTokens >= _stateProvider.State.MaxTokens)
        {
            _refillTimer?.Dispose();
            _refillTimer = null;
            return Task.CompletedTask;
        }

        if (DateTimeOffset.UtcNow - _stateProvider.State.LastRefillTime < TimeSpan.FromMinutes(1))
        {
            return Task.CompletedTask;
        }

        var minutesLeftAfterLastRefill = (DateTimeOffset.UtcNow - _stateProvider.State.LastRefillTime).TotalMinutes;
        var refillTokens = Math.Min(
            (_refillRatePerMinute * (uint)minutesLeftAfterLastRefill) + _stateProvider.State.RemainingTokens,
            _stateProvider.State.MaxTokens);

        _stateProvider.State.RemainingTokens = refillTokens;
        _stateProvider.State.LastRefillTime = DateTimeOffset.UtcNow;
        _isDirty = true;

        return Task.CompletedTask;
    }

    private void EnsureOrStartStateWriteTimer()
    {
        if (_stateWriteTimer is not null) return;
        _stateWriteTimer = this.RegisterGrainTimer(
            callback: WriteStateIfDirtyAsync,
            options: new(
                dueTime: TimeSpan.FromMilliseconds(200),
                period: TimeSpan.FromSeconds(30)));
    }

    private async Task WriteStateIfDirtyAsync(CancellationToken token)
    {
        if (_isDirty is false) return;

        await _stateProvider.WriteStateAsync(token).ConfigureAwait(true);
        _isDirty = false;
    }
}
