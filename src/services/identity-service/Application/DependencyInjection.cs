using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using IdentityService.Application;
using IdentityService.Application.Commands;
using IdentityService.Application.Options;
using IdentityService.Application.Queries;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// identity-service 依赖注入注册。
/// </summary>
public static class IdentityServiceDependencyInjection
{
    /// <summary>注册 identity-service 全部服务（配置 / 数据库 / 认证 / CQRS 处理器 / 当前用户）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddIdentityService(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations();

        // JWT 选项（单例实例，供 JwtTokenService 与 Handler 共用）
        var jwtOptions = new JwtOptions();
        configuration.GetSection("Jwt").Bind(jwtOptions);
        services.AddSingleton(jwtOptions);
        services.AddScoped<JwtTokenService>();

        // 数据库
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:IdentityDb");
        services.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(connectionString));

        // 时间与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();

        // 中介者 + CQRS 处理器（按程序集扫描注册）
        services.AddMediator();
        services.AddCqrsHandlers(typeof(RegisterUserCommandHandler).Assembly);

        return services;
    }
}
