using BuildingBlocks.Core.Results;

namespace BuildingBlocks.Communication;

/// <summary>
/// 服务间通信客户端接口 — HTTP / gRPC 可切换（Strategy 模式）。
/// </summary>
public interface IServiceClient
{
    Task<Result<T>> GetAsync<T>(string path, CancellationToken ct = default);
    Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken ct = default);
    Task<Result<T>> PutAsync<T>(string path, object body, CancellationToken ct = default);
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
    public required string ServiceName { get; init; }
    public required string BaseUrl { get; init; }
    public CommunicationProtocol Protocol { get; init; } = CommunicationProtocol.Http;
}
