using LogisticsService.Domain.Entities;
using LogisticsService.DTOs;

namespace LogisticsService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class ShipmentMapper
{
    /// <summary>运单实体转响应 DTO</summary>
    /// <param name="shipment">运单实体</param>
    /// <param name="includeTracks">是否包含轨迹列表</param>
    /// <returns>运单响应</returns>
    public static ShipmentResponse ToResponse(Shipment shipment, bool includeTracks) => new()
    {
        Id = shipment.Id,
        MerchantId = shipment.MerchantId,
        SubOrderId = shipment.SubOrderId,
        OrderNo = shipment.OrderNo,
        CarrierCode = shipment.CarrierCode,
        CarrierName = shipment.CarrierName,
        TrackingNo = shipment.TrackingNo,
        Status = shipment.Status,
        SignedAt = shipment.SignedAt,
        Tracks = includeTracks
            ? shipment.Tracks.OrderBy(t => t.TrackedAt).Select(ToTrackResponse).ToList()
            : [],
        CreatedAt = shipment.CreatedAt,
    };

    /// <summary>轨迹实体转响应 DTO</summary>
    /// <param name="track">轨迹实体</param>
    /// <returns>轨迹响应</returns>
    public static TrackResponse ToTrackResponse(ShipmentTrack track) => new()
    {
        Status = track.Status,
        Description = track.Description,
        Location = track.Location,
        TrackedAt = track.TrackedAt,
    };

    /// <summary>物流公司实体转响应 DTO</summary>
    /// <param name="company">物流公司实体</param>
    /// <returns>公司响应</returns>
    public static CompanyResponse ToCompanyResponse(LogisticsCompany company) => new()
    {
        Id = company.Id,
        Code = company.Code,
        Name = company.Name,
        TrackingUrlTemplate = company.TrackingUrlTemplate,
        IsEnabled = company.IsEnabled,
    };
}
