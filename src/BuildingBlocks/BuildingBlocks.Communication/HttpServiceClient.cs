using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Communication;

/// <summary>
/// HTTP 服务客户端实现（Strategy 模式 — HTTP 策略）。
/// </summary>
public class HttpServiceClient(HttpClient httpClient) : IServiceClient
{
    private readonly HttpClient _httpClient = httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<T>> GetAsync<T>(string path, CancellationToken ct = default)
        => await HandleResponseAsync<T>(await _httpClient.GetAsync(path, ct), ct);

    public async Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken ct = default)
        => await HandleResponseAsync<T>(await _httpClient.PostAsJsonAsync(path, body, JsonOptions, ct), ct);

    public async Task<Result<T>> PutAsync<T>(string path, object body, CancellationToken ct = default)
        => await HandleResponseAsync<T>(await _httpClient.PutAsJsonAsync(path, body, JsonOptions, ct), ct);

    public async Task<Result<T>> DeleteAsync<T>(string path, CancellationToken ct = default)
        => await HandleResponseAsync<T>(await _httpClient.DeleteAsync(path, ct), ct);

    private static async Task<Result<T>> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return Result.Failure<T>($"HTTP {response.StatusCode}: {error}", response.StatusCode.ToString());
        }

        var content = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return Result.Success(content!);
    }
}

/// <summary>
/// 通信模块 DI 注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册服务客户端 — 按服务名注册，支持 HTTP/gRPC 切换（Strategy 模式）。
    /// </summary>
    public static IServiceCollection AddServiceClient(
        this IServiceCollection services,
        string serviceName,
        string baseUrl,
        CommunicationProtocol protocol = CommunicationProtocol.Http)
    {
        if (protocol == CommunicationProtocol.Http)
        {
            services.AddHttpClient<IServiceClient, HttpServiceClient>(serviceName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            // gRPC 策略（JSON-gRPC 模式）：单例封装 GrpcChannel
            services.AddSingleton<IServiceClient>(_ => new GrpcServiceClient(baseUrl));
        }

        return services;
    }
}
