using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using NotificationService.Application.Commands;
using NotificationService.Application.Hubs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// notification-service 依赖注入注册。
/// </summary>
public static class NotificationServiceDependencyInjection
{
    /// <summary>注册 notification-service 全部服务（配置 / 数据库 / 发送器 / SignalR / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddNotificationService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库（MMP_Notification 独立库）
        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:NotificationDb");
        services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(connectionString));

        // 时间与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();

        // 渠道配置
        services.AddOptions<NotificationOptions>()
            .BindConfiguration(NotificationOptions.SectionName);

        // 发送器与实时推送
        services.AddScoped<NotificationSender>();
        services.AddScoped<SmsSender>();
        services.AddScoped<PushSender>();
        services.AddSingleton<NotificationDispatcher>();
        services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(SendInAppNotificationCommandHandler).Assembly);

        return services;
    }

    /// <summary>初始化默认通知模板（仅当模板表为空时写入，幂等）</summary>
    /// <param name="db">通知数据库上下文</param>
    /// <returns>任务</returns>
    public static async Task SeedDefaultTemplatesAsync(NotificationDbContext db)
    {
        if (await db.Templates.AsNoTracking().AnyAsync())
            return;

        var defaults = new[]
        {
            new NotificationTemplate("ORDER_PAID", "订单支付成功",
                "您的订单 {OrderNo} 已支付成功，金额 ¥{Amount}，商家将尽快发货。",
                NotificationChannel.InApp | NotificationChannel.Sms | NotificationChannel.Push, "订单支付成功通知（买家）"),
            new NotificationTemplate("ORDER_SHIPPED", "订单已发货",
                "您的订单 {OrderNo} 已由 {Company} 发货，运单号 {TrackingNo}，请注意查收。",
                NotificationChannel.InApp | NotificationChannel.Sms | NotificationChannel.Push, "订单发货通知（买家）"),
            new NotificationTemplate("ORDER_CREATED", "您有新的订单",
                "您有新订单 {OrderNo}，金额 ¥{Amount}，请及时处理。",
                NotificationChannel.InApp | NotificationChannel.Push, "新订单提醒（商户）"),
            new NotificationTemplate("PAYMENT_REFUNDED", "退款成功",
                "订单 {OrderNo} 退款成功，退款金额 ¥{Amount} 将原路退回，请注意查收。",
                NotificationChannel.InApp | NotificationChannel.Sms | NotificationChannel.Push, "退款成功通知（买家）"),
            new NotificationTemplate("SYSTEM_ANNOUNCEMENT", "平台公告",
                "{Content}",
                NotificationChannel.InApp, "平台公告（系统）"),
            new NotificationTemplate("RISK_ALERT", "风控告警",
                "风控规则「{RuleName}」命中 {Hits} 次（场景 {Scene}），请及时处置。",
                NotificationChannel.InApp | NotificationChannel.Push, "风控规则命中告警（管理员）"),
            new NotificationTemplate("MONITOR_ALERT", "监控告警",
                "服务 {ServiceName} 指标异常：{Metric}={Value}（阈值 {Threshold}），请检查。",
                NotificationChannel.InApp | NotificationChannel.Push, "性能/日志监控告警（管理员）"),
            new NotificationTemplate("SMS_VERIFY_CODE", "短信验证码",
                "您的验证码是 {Code}，{Minutes} 分钟内有效，请勿泄露。",
                NotificationChannel.Sms, "短信验证码（SMS 渠道专用）"),
        };

        db.Templates.AddRange(defaults);
        await db.SaveChangesAsync();
    }
}
