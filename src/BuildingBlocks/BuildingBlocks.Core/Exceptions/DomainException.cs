namespace BuildingBlocks.Core.Exceptions;

/// <summary>
/// 领域异常基类 — 携带错误码，区别于系统异常。
/// </summary>
public class DomainException(string message, string errorCode = "DOMAIN_ERROR", Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// 实体未找到异常。
/// </summary>
public class NotFoundException(string entityName, object key)
    : DomainException($"{entityName} 未找到，Key: {key}", "NOT_FOUND")
{
}

/// <summary>
/// 数据验证异常。
/// </summary>
public class ValidationException(IDictionary<string, string[]> errors)
    : DomainException("数据验证失败", "VALIDATION_ERROR")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>
/// 并发冲突异常。
/// </summary>
public class ConcurrencyException(string message)
    : DomainException(message, "CONCURRENCY_CONFLICT")
{
}
