using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// CQRS 处理器依赖注入注册 — 按程序集自动扫描并注册命令/查询处理器。
/// </summary>
public static class CqrsServiceCollectionExtensions
{
    /// <summary>扫描指定程序集，注册全部 ICommandHandler / IQueryHandler 实现（Scoped）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="assemblies">要扫描的程序集</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType)
                        continue;

                    var def = iface.GetGenericTypeDefinition();
                    if (def == typeof(ICommandHandler<,>)
                        || def == typeof(ICommandHandler<>)
                        || def == typeof(IQueryHandler<,>))
                    {
                        services.AddScoped(iface, type);
                    }
                }
            }
        }
        return services;
    }
}
