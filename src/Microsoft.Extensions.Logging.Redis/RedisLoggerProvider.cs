using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Microsoft.Extensions.Logging.Redis;

internal sealed class RedisLoggerProvider : ILoggerProvider
{
    private readonly string _listKey;
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly LogLevel _minimumLevel;
    private readonly ConcurrentDictionary<string, RedisLogger> _loggers = new(StringComparer.Ordinal);
    private bool _disposed;

    internal LogLevel MinimumLevel => _minimumLevel;

    public RedisLoggerProvider(
        string connectionString,
        string listKey,
        LogLevel minimumLevel = LogLevel.Trace,
        IRedisConnectionFactory? connectionFactory = null)
    {
        if (connectionString is null)
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("The connection string must be provided.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(listKey))
        {
            throw new ArgumentException("The list key must be provided.", nameof(listKey));
        }

        _listKey = listKey;
        _minimumLevel = minimumLevel;
        var factory = connectionFactory ?? DefaultRedisConnectionFactory.Instance;
        _connection = factory.Connect(connectionString);
        _database = _connection.GetDatabase();
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RedisLoggerProvider));
        }

        return _loggers.GetOrAdd(categoryName, static (name, provider) => new RedisLogger(provider, name), this);
    }

    internal void Write(RedisValue value)
    {
        if (_disposed)
        {
            return;
        }

        _database.ListRightPush(_listKey, value);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.Dispose();
        _loggers.Clear();
    }
}
