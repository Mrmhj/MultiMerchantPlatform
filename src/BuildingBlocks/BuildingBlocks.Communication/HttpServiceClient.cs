using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Communication;

/// <summary>
/// HTTP 服务客户端实现。
/// </summary>
public class HttpServiceClient : IServiceClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(path, ct);
        return await HandleResponse<T>(response, ct);
    }

    public async Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(path, body, JsonOptions, ct);
        return await HandleResponse<T>(response, ct);
    }

    public async Task<Result<T>> PutAsync<T>(string path, object body, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync(path, body, JsonOptions, ct);
        return await HandleResponse<T>(response, ct);
    }

    public async Task<Result<T>> DeleteAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync(path, ct);
        return await HandleResponse<T>(response, ct);
    }

    private static async Task<Result<T>> HandleResponse<T>(HttpResponseMessage response, CancellationToken ct)
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
    /// 注册服务客户端 — 按服务名注册 HttpClient，支持 HTTP/gRPC 切换。
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

        // gRPC 实现在需要时添加
        // if (protocol == CommunicationProtocol.Grpc) { ... }

        return services;
    }
}
