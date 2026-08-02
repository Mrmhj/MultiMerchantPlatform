using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using RiskService.Application.Commands;
using RiskService.Application.Services;
using RiskService.Domain.Entities;
using RiskService.Domain.Enums;
using RiskService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// risk-service 依赖注入注册。
/// </summary>
public static class RiskServiceDependencyInjection
{
    /// <summary>注册 risk-service 全部服务（配置 / 数据库 / 规则引擎 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddRiskService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库（MMP_Risk 独立库）
        var connectionString = configuration.GetConnectionString("RiskDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:RiskDb");
        services.AddDbContext<RiskDbContext>(o => o.UseSqlServer(connectionString));

        // 时间与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();

        // 规则引擎
        services.AddScoped<RiskRuleEngine>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(SubmitRiskEventsCommandHandler).Assembly);

        return services;
    }

    /// <summary>初始化默认风控规则（仅当规则表为空时写入，幂等）</summary>
    /// <param name="db">风控数据库上下文</param>
    /// <returns>任务</returns>
    public static async Task SeedDefaultRulesAsync(RiskDbContext db)
    {
        if (await db.RiskRules.AsNoTracking().AnyAsync())
            return;

        // 反刷单典型规则：同用户/同 IP 高频下单、高频领券、高频登录失败、高频评价
        var defaults = new[]
        {
            new RiskRule("高频下单（同用户）", "ORDER_SUBMIT", RiskDimension.User, 60, 5,
                RiskDisposition.Watch, null, "同一用户 60 秒内提交订单 ≥ 5 次，疑似刷单，观察"),
            new RiskRule("高频下单（同 IP）", "ORDER_SUBMIT", RiskDimension.Ip, 60, 10,
                RiskDisposition.Block, null, "同一 IP 60 秒内提交订单 ≥ 10 次，疑似批量刷单，拦截"),
            new RiskRule("高频领券（同用户）", "COUPON_CLAIM", RiskDimension.User, 60, 10,
                RiskDisposition.Watch, null, "同一用户 60 秒内领券 ≥ 10 次，疑似薅羊毛，观察"),
            new RiskRule("高频登录失败（同 IP）", "LOGIN_FAIL", RiskDimension.Ip, 300, 10,
                RiskDisposition.Block, null, "同一 IP 5 分钟内登录失败 ≥ 10 次，疑似撞库，拦截"),
            new RiskRule("高频评价（同用户）", "REVIEW_SUBMIT", RiskDimension.User, 60, 5,
                RiskDisposition.Watch, null, "同一用户 60 秒内提交评价 ≥ 5 次，疑似刷评，观察"),
        };

        db.RiskRules.AddRange(defaults);
        await db.SaveChangesAsync();
    }
}
