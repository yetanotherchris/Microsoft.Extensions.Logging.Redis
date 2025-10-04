using System;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Microsoft.Extensions.Logging.Redis.Tests.Unit;

public class RedisLoggerTests
{
    [Fact]
    public void Log_WithLogLevelNone_DoesNotWriteToRedis()
    {
        var (logger, database) = CreateLogger();

        logger.Log(LogLevel.None, new EventId(1), "state", null, (s, e) => "message");

        database.DidNotReceiveWithAnyArgs().ListRightPush(default(RedisKey), default(RedisValue), default, default);
    }

    [Fact]
    public void IsEnabled_ReturnsTrueForInformationAndFalseForDebugWhenInformationMinimum()
    {
        var (logger, _) = CreateLogger();

        logger.IsEnabled(LogLevel.Information).ShouldBeTrue();
        logger.IsEnabled(LogLevel.Debug).ShouldBeFalse();
        logger.IsEnabled(LogLevel.None).ShouldBeFalse();
    }

    [Fact]
    public void Log_WithNullFormatter_ThrowsArgumentNullException()
    {
        var (logger, _) = CreateLogger();

        Should.Throw<ArgumentNullException>(() => logger.Log<string>(LogLevel.Information, new EventId(42), "state", null, null!));
    }

    private static (RedisLogger logger, IDatabase database) CreateLogger()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        multiplexer.GetDatabase().Returns(database);

        var factory = Substitute.For<IRedisConnectionFactory>();
        factory.Connect(Arg.Any<string>()).Returns(multiplexer);

        var provider = new RedisLoggerProvider("localhost:6379", "logs", LogLevel.Information, factory);
        var logger = (RedisLogger)provider.CreateLogger("TestCategory");
        return (logger, database);
    }
}
