namespace DistRateLimiter.Core.Features;

[GenerateSerializer]
public sealed record GrainResult<T>
{
    public static GrainResult<T> Success(T value) => new(true, value, null);
    public static GrainResult<T> Failure(string errorMessage) => new(false, default!, errorMessage);

    [Id(0)]
    public bool IsSuccess { get; }
    [Id(1)]
    public T Value { get; }
    [Id(2)]
    public string? ErrorMessage { get; }

    private GrainResult(bool isSuccess, T value, string? errorMessage)
    {
        Value = value;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }
}
