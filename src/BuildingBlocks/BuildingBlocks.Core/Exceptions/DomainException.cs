namespace BuildingBlocks.Core.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message, string errorCode = "DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR")
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} 未找到，Key: {key}", "NOT_FOUND")
    {
    }
}

public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("数据验证失败", "VALIDATION_ERROR")
    {
        Errors = errors;
    }
}

public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message)
        : base(message, "CONCURRENCY_CONFLICT")
    {
    }
}
