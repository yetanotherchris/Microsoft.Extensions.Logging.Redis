using System;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Microsoft.Extensions.Logging.Redis.Tests.Unit;

public class RedisLoggerProviderTests
{
    [Fact]
    public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new RedisLoggerProvider(null!, "logs"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithEmptyListKey_ThrowsArgumentException(string listKey)
    {
        Should.Throw<ArgumentException>(() => new RedisLoggerProvider("localhost", listKey));
    }

    [Fact]
    public void CreateLogger_ReturnsSameInstanceForSameCategory()
    {
        var (provider, _) = CreateProvider();

        var logger1 = provider.CreateLogger("MyCategory");
        var logger2 = provider.CreateLogger("MyCategory");

        logger1.ShouldBeSameAs(logger2);
    }

    [Fact]
    public void Dispose_DisposesConnectionMultiplexer_AndLoggingAfterDisposeDoesNotThrow()
    {
        var (provider, db) = CreateProvider();
        var logger = provider.CreateLogger("DisposedCategory");

        provider.Dispose();

        Should.NotThrow(() => logger.Log(LogLevel.Information, new EventId(1), "state", null, (s, e) => "message"));

        db.DidNotReceiveWithAnyArgs().ListRightPush(default(RedisKey), default(RedisValue), default, default);
    }

    private static (RedisLoggerProvider provider, IDatabase database) CreateProvider()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        multiplexer.GetDatabase().Returns(database);

        var factory = Substitute.For<IRedisConnectionFactory>();
        factory.Connect(Arg.Any<string>()).Returns(multiplexer);

        var provider = new RedisLoggerProvider("localhost:6379", "logs", LogLevel.Trace, factory);
        return (provider, database);
    }
}
