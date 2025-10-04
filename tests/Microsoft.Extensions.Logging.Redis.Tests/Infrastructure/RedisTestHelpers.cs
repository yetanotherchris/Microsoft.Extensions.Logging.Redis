using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging.Redis.Tests.Infrastructure;

public static class RedisTestHelpers
{
    public static ILogger CreateLogger(string connectionString, string listKey, string? category = null)
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddRedis(connectionString, listKey);
        });

        return factory.CreateLogger(category ?? typeof(RedisTestHelpers).FullName!);
    }

    internal static RedisLogEntry Deserialize(string payload)
    {
        return JsonSerializer.Deserialize<RedisLogEntry>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Failed to deserialize log entry.");
    }
}
