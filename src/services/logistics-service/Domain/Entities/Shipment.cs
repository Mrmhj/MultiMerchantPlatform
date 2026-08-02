using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using LogisticsService.Domain.Enums;

namespace LogisticsService.Domain.Entities;

/// <summary>
/// 运单 — 商户发货的产物（子订单维度，一个子订单一条运单）。
/// 多租户：商户维度（MerchantId）隔离；买家维度（BuyerUserId）由 Handler 显式过滤。
/// 轨迹推进状态机：Created → InTransit → OutForDelivery → Signed，任意状态可转 Exception。
/// </summary>
public sealed class Shipment : MultiTenantEntity
{
    private readonly List<ShipmentTrack> _tracks = [];

    private Shipment() { } // EF Core

    /// <summary>创建运单（商户发货时由 order-service 内部回调创建）</summary>
    /// <param name="buyerUserId">买家用户 ID</param>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="subOrderId">子订单 ID（唯一）</param>
    /// <param name="orderId">主订单 ID</param>
    /// <param name="orderNo">订单号（快照）</param>
    /// <param name="carrierCode">物流公司编码</param>
    /// <param name="carrierName">物流公司名称（快照）</param>
    /// <param name="trackingNo">运单号</param>
    /// <param name="now">创建时间（UTC）</param>
    [SetsRequiredMembers]
    public Shipment(Guid buyerUserId, Guid merchantId, Guid subOrderId, Guid orderId,
        string orderNo, string carrierCode, string carrierName, string trackingNo, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(carrierCode) || carrierCode.Length > 50)
            throw new DomainException("物流公司编码非法", "INVALID_CARRIER_CODE");
        if (string.IsNullOrWhiteSpace(trackingNo) || trackingNo.Length > 64)
            throw new DomainException("运单号非法", "INVALID_TRACKING_NO");

        BuyerUserId = buyerUserId;
        MerchantId = merchantId;
        SubOrderId = subOrderId;
        OrderId = orderId;
        OrderNo = (orderNo ?? string.Empty).Trim();
        CarrierCode = carrierCode.Trim();
        CarrierName = (carrierName ?? string.Empty).Trim();
        TrackingNo = trackingNo.Trim();
        Status = ShipmentStatus.Created;
        CreatedAt = now;

        // 初始轨迹：商家已发货，等待物流揽收
        AddTrack(new ShipmentTrack(Id, ShipmentStatus.Created, "商家已发货，等待物流公司揽收", null, now));
    }

    /// <summary>买家用户 ID（买家维度隔离）</summary>
    public Guid BuyerUserId { get; private set; }

    /// <summary>子订单 ID（一个子订单仅一条运单）</summary>
    public Guid SubOrderId { get; private set; }

    /// <summary>主订单 ID</summary>
    public Guid OrderId { get; private set; }

    /// <summary>订单号（快照）</summary>
    public string OrderNo { get; private set; } = string.Empty;

    /// <summary>物流公司编码</summary>
    public string CarrierCode { get; private set; } = string.Empty;

    /// <summary>物流公司名称（快照）</summary>
    public string CarrierName { get; private set; } = string.Empty;

    /// <summary>运单号</summary>
    public string TrackingNo { get; private set; } = string.Empty;

    /// <summary>运单状态</summary>
    public ShipmentStatus Status { get; private set; }

    /// <summary>签收时间（未签收为 null）</summary>
    public DateTime? SignedAt { get; private set; }

    /// <summary>轨迹记录（按时间升序）</summary>
    public IReadOnlyList<ShipmentTrack> Tracks => _tracks;

    /// <summary>添加轨迹记录（仅供领域方法内部使用）</summary>
    /// <param name="track">轨迹记录</param>
    public void AddTrack(ShipmentTrack track) => _tracks.Add(track);

    /// <summary>
    /// 推进轨迹状态（模拟物流公司推送）：
    /// Created → InTransit → OutForDelivery → Signed；Exception 恢复后回到 InTransit。
    /// </summary>
    /// <param name="description">轨迹描述</param>
    /// <param name="location">地点（可选）</param>
    /// <param name="now">轨迹时间（UTC）</param>
    /// <returns>新增的轨迹记录</returns>
    /// <exception cref="DomainException">已签收或状态非法时抛出</exception>
    public ShipmentTrack Advance(string description, string? location, DateTime now)
    {
        if (Status == ShipmentStatus.Signed)
            throw new DomainException("运单已签收，无法继续推进", "SHIPMENT_ALREADY_SIGNED");

        var next = Status switch
        {
            ShipmentStatus.Created => ShipmentStatus.InTransit,
            ShipmentStatus.InTransit => ShipmentStatus.OutForDelivery,
            ShipmentStatus.OutForDelivery => ShipmentStatus.Signed,
            ShipmentStatus.Exception => ShipmentStatus.InTransit,
            _ => throw new DomainException("运单状态不支持推进", "INVALID_SHIPMENT_STATUS"),
        };

        Status = next;
        if (next == ShipmentStatus.Signed)
            SignedAt = now;

        var track = new ShipmentTrack(Id, next,
            string.IsNullOrWhiteSpace(description) ? StatusDescription(next) : description.Trim(),
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(), now);
        AddTrack(track);
        return track;
    }

    /// <summary>标记异常（滞留/拒收等，任何非终态可标记）</summary>
    /// <param name="description">异常描述</param>
    /// <param name="location">地点（可选）</param>
    /// <param name="now">轨迹时间（UTC）</param>
    /// <returns>新增的轨迹记录</returns>
    /// <exception cref="DomainException">已签收或已处于异常状态时抛出</exception>
    public ShipmentTrack MarkException(string description, string? location, DateTime now)
    {
        if (Status is ShipmentStatus.Signed or ShipmentStatus.Exception)
            throw new DomainException("当前状态不允许标记异常", "INVALID_SHIPMENT_STATUS");

        Status = ShipmentStatus.Exception;
        var track = new ShipmentTrack(Id, ShipmentStatus.Exception,
            string.IsNullOrWhiteSpace(description) ? "物流异常，请联系客服" : description.Trim(),
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(), now);
        AddTrack(track);
        return track;
    }

    /// <summary>状态默认描述</summary>
    /// <param name="status">状态</param>
    /// <returns>描述文案</returns>
    private static string StatusDescription(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Created => "商家已发货，等待物流公司揽收",
        ShipmentStatus.InTransit => "快件运输中",
        ShipmentStatus.OutForDelivery => "快递员派送中，请保持电话畅通",
        ShipmentStatus.Signed => "快件已签收，感谢使用",
        ShipmentStatus.Exception => "物流异常，请联系客服",
        _ => "物流状态更新",
    };
}
