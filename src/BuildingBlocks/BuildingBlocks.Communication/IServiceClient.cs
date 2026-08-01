using BuildingBlocks.Core.Results;

namespace BuildingBlocks.Communication;

/// <summary>
/// 服务间通信客户端接口 — HTTP / gRPC 可切换。
/// </summary>
public interface IServiceClient
{
    /// <summary>GET 请求</summary>
    Task<Result<T>> GetAsync<T>(string path, CancellationToken ct = default);

    /// <summary>POST 请求</summary>
    Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken ct = default);

    /// <summary>PUT 请求</summary>
    Task<Result<T>> PutAsync<T>(string path, object body, CancellationToken ct = default);

    /// <summary>DELETE 请求</summary>
    Task<Result<T>> DeleteAsync<T>(string path, CancellationToken ct = default);
}

/// <summary>
/// 通信协议类型。
/// </summary>
public enum CommunicationProtocol
{
    Http,
    Grpc
}

/// <summary>
/// 服务端点配置。
/// </summary>
public record ServiceEndpoint
{
    public string ServiceName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public CommunicationProtocol Protocol { get; init; } = CommunicationProtocol.Http;
}
