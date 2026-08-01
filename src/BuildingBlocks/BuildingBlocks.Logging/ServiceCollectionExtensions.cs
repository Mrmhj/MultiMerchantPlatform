using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Logging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加中心化日志 — 一行接入 logging-service。
    /// </summary>
    public static ILoggingBuilder AddCentralizedLogging(
        this ILoggingBuilder builder,
        string serviceName,
        string loggingServiceUrl = "http://localhost:8011")
    {
        builder.Services.AddHttpClient<CentralizedLoggerProvider>(client =>
        {
            client.BaseAddress = new Uri(loggingServiceUrl);
        });

        builder.Services.AddSingleton<ILoggerProvider>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(CentralizedLoggerProvider));
            return new CentralizedLoggerProvider(serviceName, httpClient);
        });

        return builder;
    }
}
