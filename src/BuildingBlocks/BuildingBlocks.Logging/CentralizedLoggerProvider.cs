using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Logging;

/// <summary>
/// 中心化日志 Provider — 异步批量上报到 logging-service（Observer 模式）。
/// </summary>
public sealed class CentralizedLoggerProvider : ILoggerProvider
{
    private readonly string _serviceName;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, CentralizedLogger> _loggers = new();
    private readonly ConcurrentQueue<LogEntry> _buffer = new();
    private readonly Timer _flushTimer;

    public CentralizedLoggerProvider(string serviceName, HttpClient httpClient)
    {
        _serviceName = serviceName;
        _httpClient = httpClient;
        _flushTimer = new Timer(_ => _ = FlushBatchAsync(), null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new CentralizedLogger(_serviceName, name, _buffer));

    private async Task FlushBatchAsync()
    {
        if (_buffer.IsEmpty) return;

        var batch = new List<LogEntry>();
        while (_buffer.TryDequeue(out var entry))
            batch.Add(entry);

        if (batch.Count == 0) return;

        try
        {
            await _httpClient.PostAsJsonAsync("/api/logs/batch", batch);
        }
        catch
        {
            // 上报失败不影响业务，重新入队下次重试
            foreach (var entry in batch)
                _buffer.Enqueue(entry);
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        _loggers.Clear();
    }
}

/// <summary>
/// 中心化日志记录器。
/// </summary>
public sealed class CentralizedLogger(string serviceName, string category, ConcurrentQueue<LogEntry> buffer) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        buffer.Enqueue(new LogEntry
        {
            ServiceName = serviceName,
            Level = logLevel.ToString(),
            Message = formatter(state, exception),
            Exception = exception?.ToString(),
            Category = category,
            Timestamp = DateTime.UtcNow
        });
    }
}
