using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Redis.Tests.Infrastructure;
using Shouldly;
using Xunit;

namespace Microsoft.Extensions.Logging.Redis.Tests.Integration;

public class BasicLoggingTests : RedisIntegrationTestBase
{
    [Fact]
    public async Task LogInformation_WritesToRedisListWithCorrectKey()
    {
        SkipIfDockerUnavailable();
        var listKey = $"test-logs:{Guid.NewGuid():N}";
        var logger = RedisTestHelpers.CreateLogger(GetConnectionString(), listKey, "BasicLogging");

        logger.LogInformation("Hello Redis");

        (await WaitForLogCountAsync(listKey, 1, TimeSpan.FromSeconds(10))).ShouldBeTrue();
        var logs = await GetLogsFromRedisAsync(listKey);
        logs.Count.ShouldBe(1);

        var entry = RedisTestHelpers.Deserialize(logs.Single());
        entry.Level.ShouldBe("Information");
        entry.Category.ShouldBe("BasicLogging");
        entry.Message.ShouldBe("Hello Redis");
        entry.Timestamp.ShouldBeLessThan(DateTimeOffset.UtcNow.AddSeconds(1));
        entry.Timestamp.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(-10));
    }

    [Fact]
    public async Task LogMultipleLevels_AllAppearInRedis()
    {
        SkipIfDockerUnavailable();
        var listKey = $"multi-logs:{Guid.NewGuid():N}";
        var logger = RedisTestHelpers.CreateLogger(GetConnectionString(), listKey, "MultiLevel");

        logger.LogTrace("Trace message");
        logger.LogDebug("Debug message");
        logger.LogInformation("Info message");
        logger.LogWarning("Warn message");
        logger.LogError("Error message");
        logger.LogCritical("Critical message");

        (await WaitForLogCountAsync(listKey, 6, TimeSpan.FromSeconds(10))).ShouldBeTrue();
        var logs = await GetLogsFromRedisAsync(listKey);
        logs.Count.ShouldBe(6);

        var entries = logs.Select(RedisTestHelpers.Deserialize).ToArray();
        var levels = entries.Select(e => e.Level).ToArray();
        levels.ShouldBe(new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" });
    }

    [Fact]
    public async Task LogWithCategory_IncludesCategoryInEntry()
    {
        SkipIfDockerUnavailable();
        var category = "MyApp.Services";
        var listKey = $"category-logs:{Guid.NewGuid():N}";
        var logger = RedisTestHelpers.CreateLogger(GetConnectionString(), listKey, category);

        logger.LogInformation("Category message");

        (await WaitForLogCountAsync(listKey, 1, TimeSpan.FromSeconds(10))).ShouldBeTrue();
        var entry = RedisTestHelpers.Deserialize((await GetLogsFromRedisAsync(listKey)).Single());
        entry.Category.ShouldBe(category);
    }

    [Fact]
    public async Task LogWithEventId_IncludesEventIdInEntry()
    {
        SkipIfDockerUnavailable();
        var listKey = $"event-logs:{Guid.NewGuid():N}";
        var logger = RedisTestHelpers.CreateLogger(GetConnectionString(), listKey, "EventCategory");

        logger.LogInformation(new EventId(100, "UserAction"), "Event message");

        (await WaitForLogCountAsync(listKey, 1, TimeSpan.FromSeconds(10))).ShouldBeTrue();
        var entry = RedisTestHelpers.Deserialize((await GetLogsFromRedisAsync(listKey)).Single());
        entry.EventId.ShouldBe(100);
        entry.EventName.ShouldBe("UserAction");
    }
}
