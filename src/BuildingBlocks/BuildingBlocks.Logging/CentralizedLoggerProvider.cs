using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Logging;

/// <summary>
/// 中心化日志 Provider — 异步批量上报到 logging-service。
/// </summary>
public class CentralizedLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, CentralizedLogger> _loggers = new();
    private readonly ConcurrentQueue<LogEntry> _buffer = new();
    private readonly string _serviceName;
    private readonly Timer _flushTimer;
    private readonly HttpClient _httpClient;

    public CentralizedLoggerProvider(string serviceName, HttpClient httpClient)
    {
        _serviceName = serviceName;
        _httpClient = httpClient;
        _flushTimer = new Timer(FlushBatch, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName,
            name => new CentralizedLogger(_serviceName, name, _buffer));
    }

    private async void FlushBatch(object? state)
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
        _httpClient.Dispose();
        _loggers.Clear();
    }
}

/// <summary>
/// 中心化日志记录器。
/// </summary>
public class CentralizedLogger : ILogger
{
    private readonly string _serviceName;
    private readonly string _category;
    private readonly ConcurrentQueue<LogEntry> _buffer;

    public CentralizedLogger(string serviceName, string category, ConcurrentQueue<LogEntry> buffer)
    {
        _serviceName = serviceName;
        _category = category;
        _buffer = buffer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        _buffer.Enqueue(new LogEntry
        {
            ServiceName = _serviceName,
            Level = logLevel.ToString(),
            Message = formatter(state, exception),
            Exception = exception?.ToString(),
            Category = _category,
            Timestamp = DateTime.UtcNow
        });
    }
}
