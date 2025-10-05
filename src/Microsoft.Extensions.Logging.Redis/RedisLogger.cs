using System.Text.Json;

namespace Microsoft.Extensions.Logging.Redis;

internal sealed class RedisLogger : ILogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly IDisposable NoOpScope = new NoOpDisposable();

    private readonly RedisLoggerProvider _provider;
    private readonly string _categoryName;

    public RedisLogger(RedisLoggerProvider provider, string categoryName)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpScope;

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
        {
            return false;
        }

        return logLevel >= _provider.MinimumLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (formatter is null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        var message = formatter(state, exception);

        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var entry = new RedisLogEntry(
            DateTimeOffset.UtcNow,
            logLevel.ToString(),
            _categoryName,
            eventId.Id != 0 ? eventId.Id : null,
            eventId.Name,
            message,
            exception?.ToString(),
            CaptureState(state));

        var payload = JsonSerializer.Serialize(entry, SerializerOptions);
        try
        {
            _provider.Write(payload);
        }
        catch
        {
            // Swallow exceptions from Redis writes to avoid crashing the application logging pipeline.
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static object? CaptureState<TState>(TState state)
    {
        if (state is null)
        {
            return null;
        }

        if (state is IReadOnlyList<KeyValuePair<string, object?>> kvps)
        {
            return kvps
                .Where(kvp => !string.Equals(kvp.Key, "{OriginalFormat}", StringComparison.Ordinal))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        }

        if (state is IEnumerable<KeyValuePair<string, object?>> enumerable)
        {
            return enumerable
                .Where(kvp => !string.Equals(kvp.Key, "{OriginalFormat}", StringComparison.Ordinal))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        }

        return state.ToString();
    }
}
