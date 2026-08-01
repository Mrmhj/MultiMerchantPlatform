namespace BuildingBlocks.Core.Results;

/// <summary>
/// 操作结果 — 统一返回成功/失败，避免异常控制流。
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    protected Result(bool isSuccess, string? error = null, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true);
    public static Result Failure(string error, string? code = null) => new(false, error, code);
    public static Result<T> Success<T>(T value) => new(value);
    public static Result<T> Failure<T>(string error, string? code = null) => new(default, false, error, code);
}

/// <summary>
/// 带返回值的操作结果。
/// </summary>
public class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess = true, string? error = null, string? code = null)
        : base(isSuccess, error, code)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    public static implicit operator Result<T>(T value) => Success(value);
}
