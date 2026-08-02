using System.Text.Json;
using BuildingBlocks.Core.Results;
using Grpc.Core;
using Grpc.Net.Client;

namespace BuildingBlocks.Communication;

/// <summary>
/// gRPC 服务客户端实现（Strategy 模式 — gRPC 策略，JSON-gRPC 模式）。
/// 约定：path 格式 "<c>&lt;ServiceName&gt;/&lt;MethodName&gt;</c>"（如 "OrderService/GetOrder"）；
/// 请求/响应负载为 JSON 字节（服务端需使用相同 JSON Marshaller 或 gRPC JSON transcoding 端点）。
/// 适用场景：需要强契约、低延迟的内部服务间调用（区别于 HTTP 的宽松模式）。
/// </summary>
public sealed class GrpcServiceClient : IServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Marshaller<byte[]> ByteMarshaller = Marshallers.Create(b => b, b => b);

    private readonly CallInvoker _invoker;

    public GrpcServiceClient(string baseUrl)
    {
        var channel = GrpcChannel.ForAddress(baseUrl);
        _invoker = channel.CreateCallInvoker();
    }

    /// <inheritdoc />
    public Task<Result<T>> GetAsync<T>(string path, CancellationToken ct = default)
        => CallAsync<T>(path, null, ct);

    /// <inheritdoc />
    public Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken ct = default)
        => CallAsync<T>(path, body, ct);

    /// <inheritdoc />
    public Task<Result<T>> PutAsync<T>(string path, object body, CancellationToken ct = default)
        => CallAsync<T>(path, body, ct);

    /// <inheritdoc />
    public Task<Result<T>> DeleteAsync<T>(string path, CancellationToken ct = default)
        => CallAsync<T>(path, null, ct);

    private async Task<Result<T>> CallAsync<T>(string path, object? body, CancellationToken ct)
    {
        try
        {
            var parts = path.TrimStart('/').Split('/', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                return Result.Failure<T>("gRPC path 格式错误，应为 \"<ServiceName>/<MethodName>\"，如 \"OrderService/GetOrder\"");

            var method = new Method<byte[], byte[]>(
                MethodType.Unary, parts[0], parts[1], ByteMarshaller, ByteMarshaller);

            var payload = body is null ? [] : JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
            var call = _invoker.AsyncUnaryCall(method, null, new CallOptions(cancellationToken: ct), payload);
            var response = await call.ResponseAsync;

            if (response.Length == 0)
                return Result.Failure<T>("gRPC 响应为空");

            var result = JsonSerializer.Deserialize<T>(response, JsonOptions);
            return Result.Success(result!);
        }
        catch (RpcException ex)
        {
            return Result.Failure<T>($"gRPC {ex.Status.StatusCode}: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(ex.Message);
        }
    }
}
