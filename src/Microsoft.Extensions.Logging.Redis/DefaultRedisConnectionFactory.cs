using StackExchange.Redis;

namespace Microsoft.Extensions.Logging.Redis;

internal sealed class DefaultRedisConnectionFactory : IRedisConnectionFactory
{
    public static DefaultRedisConnectionFactory Instance { get; } = new();

    private DefaultRedisConnectionFactory()
    {
    }

    public IConnectionMultiplexer Connect(string connectionString) => ConnectionMultiplexer.Connect(connectionString);
}
