namespace BuildingBlocks.Core.Common;

/// <summary>
/// 防御性编程 — 参数校验工具。
/// </summary>
public static class Guard
{
    public static T NotNull<T>(T? value, string paramName) where T : class
    {
        return value ?? throw new ArgumentNullException(paramName);
    }

    public static string NotNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("值不能为空或空白。", paramName);
        return value;
    }

    public static Guid NotEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Guid 不能为空。", paramName);
        return value;
    }

    public static int Positive(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentException("值必须为正数。", paramName);
        return value;
    }
}
