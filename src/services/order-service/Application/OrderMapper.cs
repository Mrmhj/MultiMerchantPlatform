using OrderService.Domain.Entities;
using OrderService.DTOs;

namespace OrderService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class OrderMapper
{
    /// <summary>商品项实体转响应 DTO</summary>
    /// <param name="item">商品项实体</param>
    /// <returns>商品项响应</returns>
    public static OrderItemResponse ToItemResponse(OrderItem item) => new()
    {
        Id = item.Id,
        MerchantId = item.MerchantId,
        ProductName = item.ProductName,
        SkuCode = item.SkuCode,
        Spec = item.Spec,
        UnitPrice = item.UnitPrice,
        Quantity = item.Quantity,
        Subtotal = item.Subtotal,
    };

    /// <summary>子订单实体转响应 DTO</summary>
    /// <param name="sub">子订单实体</param>
    /// <returns>子订单响应</returns>
    public static SubOrderResponse ToSubOrderResponse(SubOrder sub) => new()
    {
        Id = sub.Id,
        OrderId = sub.OrderId,
        MerchantId = sub.MerchantId,
        MerchantName = sub.MerchantName,
        TotalAmount = sub.TotalAmount,
        Status = sub.Status,
        CarrierCode = sub.CarrierCode,
        TrackingNo = sub.TrackingNo,
        Items = sub.Items.Select(ToItemResponse).ToList(),
    };

    /// <summary>主订单实体转响应 DTO（含子单与商品项）</summary>
    /// <param name="order">主订单实体</param>
    /// <returns>订单响应</returns>
    public static OrderResponse ToResponse(Order order) => new()
    {
        Id = order.Id,
        OrderNo = order.OrderNo,
        BuyerUserId = order.BuyerUserId,
        TotalAmount = order.TotalAmount,
        Status = order.Status,
        Remark = order.Remark,
        SubOrders = order.SubOrders.Select(ToSubOrderResponse).ToList(),
        CreatedAt = order.CreatedAt,
    };
}
