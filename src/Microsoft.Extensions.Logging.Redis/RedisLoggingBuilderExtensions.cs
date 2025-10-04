using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging.Redis;

public static class RedisLoggingBuilderExtensions
{
    public static ILoggingBuilder AddRedis(this ILoggingBuilder builder, string connectionString, string listKey)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("The connection string must be provided.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(listKey))
        {
            throw new ArgumentException("The list key must be provided.", nameof(listKey));
        }

        builder.Services.AddSingleton<ILoggerProvider>(_ => new RedisLoggerProvider(connectionString, listKey));
        return builder;
    }
}
