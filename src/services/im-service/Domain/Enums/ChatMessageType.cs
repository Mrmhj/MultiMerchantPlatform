namespace ImService.Domain.Enums;

/// <summary>
/// 消息类型：文本 / 图片 / 文件 / 订单卡片 / 系统通知。
/// </summary>
public enum ChatMessageType
{
    /// <summary>纯文本</summary>
    Text = 1,

    /// <summary>图片（Content 存图片 URL）</summary>
    Image = 2,

    /// <summary>文件（Content 存文件 URL）</summary>
    File = 3,

    /// <summary>订单卡片（Content 存订单 JSON/卡片数据）</summary>
    OrderCard = 4,

    /// <summary>系统通知（订单/物流状态、平台公告等）</summary>
    System = 5,
}
