using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;

namespace ProductService.Domain.Entities;

/// <summary>
/// 商品分类 — 商户自建分类（支持父子层级）。
/// 多租户实体：MerchantId 强制隔离。
/// </summary>
public sealed class Category : MultiTenantEntity
{
    private Category() { } // EF Core

    /// <summary>创建分类</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="name">分类名称</param>
    /// <param name="parentId">父分类 ID（可选，空为顶级）</param>
    /// <param name="sortOrder">排序值（小在前）</param>
    [SetsRequiredMembers]
    public Category(Guid merchantId, string name, Guid? parentId = null, int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        MerchantId = merchantId;
        Name = name.Trim();
        ParentId = parentId;
        SortOrder = sortOrder;
        IsActive = true;
    }

    /// <summary>分类名称</summary>
    public string Name { get; private set; } = null!;

    /// <summary>父分类 ID（空为顶级分类）</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>排序值（小在前）</summary>
    public int SortOrder { get; private set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; private set; }

    /// <summary>重命名 / 调整层级与排序</summary>
    /// <param name="name">新名称</param>
    /// <param name="parentId">新父分类（空为顶级）</param>
    /// <param name="sortOrder">新排序值</param>
    public void Update(string name, Guid? parentId, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (parentId == Id)
            throw new InvalidOperationException("分类不能作为自己的父分类");

        Name = name.Trim();
        ParentId = parentId;
        SortOrder = sortOrder;
    }

    /// <summary>启用分类</summary>
    public void Activate() => IsActive = true;

    /// <summary>停用分类（隐藏，不删除）</summary>
    public void Deactivate() => IsActive = false;
}
