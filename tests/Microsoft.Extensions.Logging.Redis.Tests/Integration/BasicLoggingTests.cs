using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Redis.Tests.Infrastructure;
using Shouldly;
using Xunit;

namespace Microsoft.Extensions.Logging.Redis.Tests.Integration;

/// <summary>
/// Integration tests using Docker Compose managed Redis
/// </summary>
public class BasicLoggingTests : RedisIntegrationTestBase
{
    [Fact]
    public async Task LogInformation_WritesToRedisListWithCorrectKey()
    {
        SkipIfRedisUnavailable();
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
        SkipIfRedisUnavailable();
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
        SkipIfRedisUnavailable();
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
        SkipIfRedisUnavailable();
        var listKey = $"event-logs:{Guid.NewGuid():N}";
        var logger = RedisTestHelpers.CreateLogger(GetConnectionString(), listKey, "EventCategory");

        logger.LogInformation(new EventId(100, "UserAction"), "Event message");

        (await WaitForLogCountAsync(listKey, 1, TimeSpan.FromSeconds(10))).ShouldBeTrue();
        var entry = RedisTestHelpers.Deserialize((await GetLogsFromRedisAsync(listKey)).Single());
        entry.EventId.ShouldBe(100);
        entry.EventName.ShouldBe("UserAction");
    }

    [Fact]
    public async Task LogWithException_IncludesExceptionInEntry()
    {
        SkipIfRedisUnavailable();
        var listKey = $"exception-logs:{Guid.NewGuid():N}";
        var logger = RedisTestHelpers.CreateLogger(GetConnectionString(), listKey, "ExceptionCategory");

        var exception = new InvalidOperationException("Test exception");
        logger.LogError(exception, "An error occurred");

        (await WaitForLogCountAsync(listKey, 1, TimeSpan.FromSeconds(10))).ShouldBeTrue();
        var entry = RedisTestHelpers.Deserialize((await GetLogsFromRedisAsync(listKey)).Single());
        entry.Exception.ShouldNotBeNull();
        entry.Exception.ShouldContain("InvalidOperationException");
        entry.Exception.ShouldContain("Test exception");
        entry.Message.ShouldBe("An error occurred");
        entry.Level.ShouldBe("Error");
    }

    [Fact]
    public async Task ConcurrentLogging_AllEntriesAppear()
    {
        SkipIfRedisUnavailable();
        var listKey = $"concurrent-logs:{Guid.NewGuid():N}";
        var logger = RedisTestHelpers.CreateLogger(GetConnectionString(), listKey, "ConcurrentCategory");

        var tasks = Enumerable.Range(1, 10)
            .Select(i => Task.Run(() => logger.LogInformation($"Message {i}")))
            .ToArray();

        await Task.WhenAll(tasks);

        (await WaitForLogCountAsync(listKey, 10, TimeSpan.FromSeconds(10))).ShouldBeTrue();
        var logs = await GetLogsFromRedisAsync(listKey);
        logs.Count.ShouldBe(10);

        var entries = logs.Select(RedisTestHelpers.Deserialize).ToArray();
        entries.All(e => e.Level == "Information").ShouldBeTrue();
        entries.All(e => e.Category == "ConcurrentCategory").ShouldBeTrue();
    }
}