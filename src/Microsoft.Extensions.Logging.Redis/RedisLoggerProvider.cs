using System;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Microsoft.Extensions.Logging.Redis;

internal sealed class RedisLoggerProvider : ILoggerProvider
{
    private readonly string _listKey;
    private readonly ConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private bool _disposed;

    public RedisLoggerProvider(string connectionString, string listKey)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("The connection string must be provided.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(listKey))
        {
            throw new ArgumentException("The list key must be provided.", nameof(listKey));
        }

        _listKey = listKey;
        _connection = ConnectionMultiplexer.Connect(connectionString);
        _database = _connection.GetDatabase();
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RedisLoggerProvider));
        }

        return new RedisLogger(this, categoryName);
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
    }
}
