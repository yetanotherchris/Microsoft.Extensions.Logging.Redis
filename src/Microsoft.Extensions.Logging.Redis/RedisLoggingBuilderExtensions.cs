using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Logging.Redis;

/// <summary>
/// Provides extension methods for adding Redis logging to an <see cref="ILoggingBuilder"/>.
/// </summary>
public static class RedisLoggingBuilderExtensions
{
    /// <summary>
    /// Adds a Redis logger provider to the logging builder, enabling log entries to be stored in a Redis list.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <param name="connectionString">The Redis connection string used to connect to the Redis server.</param>
    /// <param name="listKey">The Redis list key where log entries will be stored.</param>
    /// <returns>The logging builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> or <paramref name="listKey"/> is null, empty, or whitespace.</exception>
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
