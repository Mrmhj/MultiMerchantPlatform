namespace BuildingBlocks.Core.Common;

/// <summary>
/// 防御性编程 — 参数校验工具，使用 C# 内置 ArgumentException.ThrowIf* 方法。
/// </summary>
public static class Guard
{
    public static T NotNull<T>(T? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    public static string NotNullOrWhiteSpace(string? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }

    public static Guid NotEmpty(Guid value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Guid 不能为空。", paramName);
        return value;
    }

    public static int Positive(int value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= 0)
            throw new ArgumentException("值必须为正数。", paramName);
        return value;
    }
}
