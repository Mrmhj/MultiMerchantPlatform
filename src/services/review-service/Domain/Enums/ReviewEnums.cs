namespace ReviewService.Domain.Enums;

/// <summary>
/// 评价状态 — 默认可见，商户可隐藏违规评价（隐藏后 C 端不可见，不计入公开评分统计）。
/// </summary>
public enum ReviewStatus
{
    /// <summary>可见（C 端展示）</summary>
    Visible = 1,

    /// <summary>已隐藏（商户操作，C 端不再展示）</summary>
    Hidden = 2,
}
