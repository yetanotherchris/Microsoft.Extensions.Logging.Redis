using StackExchange.Redis;

namespace Microsoft.Extensions.Logging.Redis;

internal interface IRedisConnectionFactory
{
    IConnectionMultiplexer Connect(string connectionString);
}
