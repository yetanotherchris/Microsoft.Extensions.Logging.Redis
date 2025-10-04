using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Docker.DotNet;
using StackExchange.Redis;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Extensions.Logging.Redis.Tests.Infrastructure;

public abstract class RedisIntegrationTestBase : IAsyncLifetime
{
    private const int RedisPort = 6379;
    protected const string DefaultListKey = "logs";

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnectionMultiplexer? _connectionMultiplexer;

    protected ITestcontainersContainer? RedisContainer { get; private set; }
    protected string ConnectionString { get; private set; } = string.Empty;
    protected bool DockerAvailable { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            var containerBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("redis:7-alpine")
                .WithPortBinding(RedisPort, assignRandomHostPort: true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "ping"))
                .WithCleanUp(true)
                .WithAutoRemove(true);

            RedisContainer = containerBuilder.Build();
            await RedisContainer.StartAsync();

            var mappedPort = RedisContainer.GetMappedPublicPort(RedisPort);
            ConnectionString = $"{RedisContainer.Hostname}:{mappedPort}";

            await WaitForRedisAsync(TimeSpan.FromSeconds(30));
            DockerAvailable = true;
        }
        catch (Exception ex) when (IsDockerUnavailable(ex))
        {
            DockerAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connectionMultiplexer is not null)
        {
            await _connectionMultiplexer.CloseAsync();
            await _connectionMultiplexer.DisposeAsync();
        }

        _connectionLock.Dispose();

        if (DockerAvailable && RedisContainer is not null)
        {
            await RedisContainer.DisposeAsync();
        }
    }

    protected string GetConnectionString() => ConnectionString;

    protected void SkipIfDockerUnavailable()
    {
        if (!DockerAvailable)
        {
            throw SkipException.ForSkip("Docker is required for integration tests.");
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

        throw new InvalidOperationException($"Redis did not become available within {timeout}.", lastError);
    }

    private static bool IsDockerUnavailable(Exception ex)
    {
        if (ex is DockerApiException or HttpRequestException or SocketException)
        {
            return true;
        }

        return ex is InvalidOperationException && ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase);
    }
}
