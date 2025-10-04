using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Extensions.Logging.Redis.Tests.Infrastructure;

/// <summary>
/// Base class for Redis integration tests using Docker Compose managed Redis instance
/// Automatically starts and stops Redis container using DockerComposeRedisManager
/// </summary>
public abstract class RedisIntegrationTestBase : IAsyncLifetime
{
    protected const string DefaultListKey = "logs";

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnectionMultiplexer? _connectionMultiplexer;
    private DockerComposeRedisManager? _redisManager;

    protected string ConnectionString => _redisManager?.ConnectionString ?? "localhost:6379";
    protected bool RedisAvailable => _redisManager?.IsAvailable ?? false;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _redisManager = await DockerComposeRedisManager.CreateAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Redis container: {ex.Message}");
            _redisManager = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connectionMultiplexer is not null)
        {
            await _connectionMultiplexer.CloseAsync();
            await _connectionMultiplexer.DisposeAsync();
        }

        if (_redisManager is not null)
        {
            await _redisManager.DisposeAsync();
        }

        _connectionLock.Dispose();
    }

    protected string GetConnectionString() => ConnectionString;

    protected void SkipIfRedisUnavailable()
    {
        if (!RedisAvailable)
        {
            throw SkipException.ForSkip("Redis container failed to start - Docker/Rancher/Podman may not be available");
        }
    }

    protected async Task<IConnectionMultiplexer> GetRedisConnectionAsync()
    {
        if (_connectionMultiplexer is { IsConnected: true })
        {
            return _connectionMultiplexer;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_connectionMultiplexer is { IsConnected: true })
            {
                return _connectionMultiplexer;
            }

            _connectionMultiplexer?.Dispose();
            _connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
            return _connectionMultiplexer;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    protected async Task ClearRedisDataAsync(string? listKey = null)
    {
        var connection = await GetRedisConnectionAsync();
        if (string.IsNullOrWhiteSpace(listKey))
        {
            foreach (var endPoint in connection.GetEndPoints())
            {
                var server = connection.GetServer(endPoint);
                await server.FlushDatabaseAsync();
            }
        }
        else
        {
            await connection.GetDatabase().KeyDeleteAsync(listKey);
        }
    }

    protected async Task<IReadOnlyList<string>> GetLogsFromRedisAsync(string listKey)
    {
        var connection = await GetRedisConnectionAsync();
        var values = await connection.GetDatabase().ListRangeAsync(listKey, 0, -1);
        return values.Select(v => v.ToString() ?? string.Empty).ToArray();
    }

    protected async Task<long> GetLogCountAsync(string listKey)
    {
        var connection = await GetRedisConnectionAsync();
        return await connection.GetDatabase().ListLengthAsync(listKey);
    }

    protected async Task<bool> WaitForLogCountAsync(string listKey, long expectedCount, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await GetLogCountAsync(listKey) == expectedCount)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return await GetLogCountAsync(listKey) == expectedCount;
    }

    internal static RedisLogEntry? DeserializeLogEntry(string payload)
    {
        return JsonSerializer.Deserialize<RedisLogEntry>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
    }

    private async Task WaitForRedisAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastError = null;
        
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
                await connection.GetDatabase().PingAsync();
                await connection.CloseAsync();
                await connection.DisposeAsync();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new InvalidOperationException($"Redis did not become available within {timeout}. Make sure Redis is running on {ConnectionString}. Use: ./test-redis.ps1 start", lastError);
    }
}