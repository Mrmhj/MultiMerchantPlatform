using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using StockService.Domain.Enums;

namespace StockService.Domain.Entities;

/// <summary>
/// 库存条目 — 按 SKU 维护总库存/预占/可用。
/// 库存公式：可用 = 总库存 - 预占；所有变动走领域方法并记录流水（充血模型）。
/// </summary>
public sealed class StockItem : MultiTenantEntity
{
    private StockItem() { } // EF Core

    /// <summary>创建库存条目（初始总库存 = total，可用 = total）</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="skuId">SKU ID（全局唯一）</param>
    /// <param name="total">初始总库存</param>
    [SetsRequiredMembers]
    public StockItem(Guid merchantId, Guid skuId, int total)
    {
        if (total < 0)
            throw new ArgumentException("库存不能为负", nameof(total));

        MerchantId = merchantId;
        SkuId = skuId;
        Total = total;
        Reserved = 0;
    }

    /// <summary>SKU ID（全局唯一）</summary>
    public Guid SkuId { get; private set; }

    /// <summary>总库存（已售 + 预占 + 可用）</summary>
    public int Total { get; private set; }

    /// <summary>已预占数量（下单未支付）</summary>
    public int Reserved { get; private set; }

    /// <summary>可用库存（可继续售卖）</summary>
    public int Available => Total - Reserved;

    /// <summary>预占库存（下单时）— 可用不足则抛出</summary>
    /// <param name="quantity">预占数量</param>
    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("数量必须大于 0", nameof(quantity));
        if (Available < quantity)
            throw new InvalidOperationException($"库存不足（可用 {Available}，需要 {quantity}）");

        Reserved += quantity;
    }

    /// <summary>确认扣减（支付后）— 预占转扣减</summary>
    /// <param name="quantity">扣减数量</param>
    public void ConfirmReservation(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("数量必须大于 0", nameof(quantity));
        if (Reserved < quantity)
            throw new InvalidOperationException($"预占不足（已预占 {Reserved}，需扣减 {quantity}）");

        Reserved -= quantity;
        Total -= quantity;
    }

    /// <summary>释放预占（取消回滚）</summary>
    /// <param name="quantity">释放数量</param>
    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("数量必须大于 0", nameof(quantity));
        if (Reserved < quantity)
            throw new InvalidOperationException($"预占不足（已预占 {Reserved}，需释放 {quantity}）");

        Reserved -= quantity;
    }

    /// <summary>补货入库</summary>
    /// <param name="quantity">补货数量</param>
    public void Increase(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("补货数量必须大于 0", nameof(quantity));

        Total += quantity;
    }

    /// <summary>写库存流水（记录每次变动）</summary>
    /// <param name="type">流水类型</param>
    /// <param name="quantity">变动数量</param>
    /// <param name="referenceId">关联业务号（订单/支付单，可选）</param>
    /// <returns>流水记录</returns>
    public StockTransaction RecordTransaction(StockTransactionType type, int quantity, string? referenceId = null)
        => new(MerchantId, SkuId, type, quantity, referenceId);
}

/// <summary>
/// 库存流水 — 每次库存变动审计记录。
/// </summary>
public sealed class StockTransaction : Entity
{
    private StockTransaction() { } // EF Core

    /// <summary>创建流水</summary>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="skuId">SKU ID</param>
    /// <param name="type">流水类型</param>
    /// <param name="quantity">变动数量</param>
    /// <param name="referenceId">关联业务号（可选）</param>
    public StockTransaction(Guid merchantId, Guid skuId, StockTransactionType type, int quantity, string? referenceId = null)
    {
        MerchantId = merchantId;
        SkuId = skuId;
        Type = type;
        Quantity = quantity;
        ReferenceId = referenceId;
    }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; private set; }

    /// <summary>流水类型（创建/预占/扣减/释放/补货）</summary>
    public StockTransactionType Type { get; private set; }

    /// <summary>变动数量</summary>
    public int Quantity { get; private set; }

    /// <summary>关联业务号（订单号/支付单号，可选）</summary>
    public string? ReferenceId { get; private set; }
}
