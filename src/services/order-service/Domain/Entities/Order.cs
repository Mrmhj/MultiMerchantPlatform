using BuildingBlocks.Core.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities;

/// <summary>
/// 主订单 — 买家维度（含多个按商户拆分的子订单）。
/// 状态机：Pending → Paid → Completed / Cancelled；金额与拆单逻辑内聚（充血模型）。
/// </summary>
public sealed class Order : Entity
{
    private readonly List<SubOrder> _subOrders = [];

    private Order() { } // EF Core

    private Order(Guid buyerUserId, string orderNo, string? remark)
    {
        BuyerUserId = buyerUserId;
        OrderNo = orderNo;
        Remark = remark?.Trim();
        Status = OrderStatus.Pending;
    }

    /// <summary>
    /// 创建订单并拆单 — 商品项按商户分组，生成多个子订单。
    /// </summary>
    /// <param name="buyerUserId">买家用户 ID</param>
    /// <param name="orderNo">业务订单号</param>
    /// <param name="items">订单商品项（可跨商户）</param>
    /// <param name="remark">买家备注（可选）</param>
    /// <returns>已拆单的主订单</returns>
    /// <exception cref="ArgumentException">商品项为空或含无效数据时抛出</exception>
    public static Order Create(Guid buyerUserId, string orderNo, IEnumerable<OrderItemInput> items, string? remark = null)
    {
        var list = items.ToList();
        if (list.Count == 0)
            throw new ArgumentException("订单商品项不能为空", nameof(items));

        var order = new Order(buyerUserId, orderNo, remark);

        // 按商户拆单：同一商户的商品归入同一个子订单
        foreach (var group in list.GroupBy(i => i.MerchantId))
        {
            var sub = new SubOrder(order.Id, group.Key, group.First().MerchantName);
            foreach (var item in group)
                sub.AddItem(item);
            order._subOrders.Add(sub);
        }

        order.TotalAmount = order._subOrders.Sum(s => s.TotalAmount);
        return order;
    }

    /// <summary>买家用户 ID</summary>
    public Guid BuyerUserId { get; private set; }

    /// <summary>业务订单号（ORD + 时间戳 + 随机）</summary>
    public string OrderNo { get; private set; } = null!;

    /// <summary>订单总金额（所有子单合计）</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>订单状态（Pending/Paid/Completed/Cancelled）</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>买家备注</summary>
    public string? Remark { get; private set; }

    /// <summary>子订单列表（按商户拆单结果）</summary>
    public IReadOnlyList<SubOrder> SubOrders => _subOrders;

    /// <summary>取消订单（仅待付款可取消，级联取消全部子单）</summary>
    /// <param name="reason">取消原因</param>
    public void Cancel(string? reason = null)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"当前状态不允许取消（{Status}）");

        Status = OrderStatus.Cancelled;
        foreach (var sub in _subOrders)
            sub.Cancel(reason);
    }

    /// <summary>标记已付款（级联子单付款）</summary>
    public void MarkPaid()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"当前状态不允许支付（{Status}）");

        Status = OrderStatus.Paid;
        foreach (var sub in _subOrders)
            sub.MarkPaid();
    }

    /// <summary>尝试完成主订单（全部子单完成后触发）</summary>
    public void TryComplete()
    {
        if (Status == OrderStatus.Paid && _subOrders.Count > 0
            && _subOrders.All(s => s.Status == SubOrderStatus.Completed))
        {
            Status = OrderStatus.Completed;
        }
    }
}

/// <summary>
/// 子订单 — 拆单结果，按商户维度独立履约（发货/完成/取消）。
/// </summary>
public sealed class SubOrder : Entity
{
    private readonly List<OrderItem> _items = [];

    private SubOrder() { } // EF Core

    /// <summary>创建子订单（初始 Pending）</summary>
    /// <param name="orderId">所属主订单 ID</param>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="merchantName">商户名称（快照）</param>
    public SubOrder(Guid orderId, Guid merchantId, string merchantName)
    {
        OrderId = orderId;
        MerchantId = merchantId;
        MerchantName = merchantName;
        Status = SubOrderStatus.Pending;
    }

    /// <summary>所属主订单 ID</summary>
    public Guid OrderId { get; private set; }

    /// <summary>商户 ID（拆单维度）</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; private set; } = null!;

    /// <summary>子单金额（商品项小计合计）</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>子单状态（Pending/Paid/Shipped/Completed/Cancelled）</summary>
    public SubOrderStatus Status { get; private set; }

    /// <summary>商品项列表</summary>
    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>添加商品项（仅供订单创建时调用）</summary>
    /// <param name="input">商品项输入</param>
    internal void AddItem(OrderItemInput input)
    {
        var item = new OrderItem(
            Id, input.MerchantId, input.ProductId, input.ProductName,
            input.SkuId, input.SkuCode, input.Spec,
            input.UnitPrice, input.Quantity);
        _items.Add(item);
        TotalAmount += item.Subtotal;
    }

    /// <summary>标记已付款</summary>
    internal void MarkPaid()
    {
        if (Status == SubOrderStatus.Pending)
            Status = SubOrderStatus.Paid;
    }

    /// <summary>取消子单（仅待付款）</summary>
    /// <param name="reason">取消原因</param>
    internal void Cancel(string? reason = null)
    {
        if (Status == SubOrderStatus.Pending)
            Status = SubOrderStatus.Cancelled;
    }

    /// <summary>发货（商户操作，已付款或已发货可发货）</summary>
    public void Ship()
    {
        if (Status is not (SubOrderStatus.Paid or SubOrderStatus.Shipped))
            throw new InvalidOperationException($"当前状态不允许发货（{Status}）");

        Status = SubOrderStatus.Shipped;
    }

    /// <summary>完成子单（已发货可完成）</summary>
    public void Complete()
    {
        if (Status != SubOrderStatus.Shipped)
            throw new InvalidOperationException($"当前状态不允许完成（{Status}）");

        Status = SubOrderStatus.Completed;
    }
}

/// <summary>
/// 订单商品项（商品/价格快照，含商户归属）。
/// </summary>
public sealed class OrderItem : Entity
{
    private OrderItem() { } // EF Core

    /// <summary>创建订单商品项</summary>
    /// <param name="subOrderId">所属子订单 ID</param>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="productName">商品名称（快照）</param>
    /// <param name="skuId">SKU ID</param>
    /// <param name="skuCode">SKU 编码（快照）</param>
    /// <param name="spec">规格（快照）</param>
    /// <param name="unitPrice">单价（快照，元）</param>
    /// <param name="quantity">数量</param>
    public OrderItem(Guid subOrderId, Guid merchantId, Guid productId, string productName,
        Guid skuId, string skuCode, string spec, decimal unitPrice, int quantity)
    {
        if (unitPrice < 0)
            throw new ArgumentException("单价不能为负", nameof(unitPrice));
        if (quantity <= 0)
            throw new ArgumentException("数量必须大于 0", nameof(quantity));

        SubOrderId = subOrderId;
        MerchantId = merchantId;
        ProductId = productId;
        ProductName = productName;
        SkuId = skuId;
        SkuCode = skuCode;
        Spec = spec;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    /// <summary>所属子订单 ID</summary>
    public Guid SubOrderId { get; private set; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; private set; }

    /// <summary>商品名称（快照）</summary>
    public string ProductName { get; private set; } = null!;

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; private set; }

    /// <summary>SKU 编码（快照）</summary>
    public string SkuCode { get; private set; } = null!;

    /// <summary>规格（快照）</summary>
    public string Spec { get; private set; } = null!;

    /// <summary>单价（快照，元）</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>数量</summary>
    public int Quantity { get; private set; }

    /// <summary>小计金额（单价 × 数量）</summary>
    public decimal Subtotal => UnitPrice * Quantity;
}

/// <summary>
/// 订单商品项输入（创建订单时提交，含商户归属以支持拆单）。
/// </summary>
public sealed record OrderItemInput(
    Guid MerchantId,
    string MerchantName,
    Guid ProductId,
    string ProductName,
    Guid SkuId,
    string SkuCode,
    string Spec,
    decimal UnitPrice,
    int Quantity);
