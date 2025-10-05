# Microsoft.Extensions.Logging.Redis

A Redis `ILogger` provider that writes log events to a Redis list using [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/).

## Usage

```csharp
var services = new ServiceCollection();
services.AddLogging(x =>
{
    x.AddRedis("localhost:6379", "my-list-key");
    x.AddSimpleConsole();
});
var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("This is a test log message");
```

The following JSON format is written to Redis as a list entry (a `RedisLogEntry` object):

```
{
    "timestamp":"2025-10-05T10:29:39.7661863+00:00",
    "level":"Information",
    "category":"Program",
    "message":"This is a test log message",
    "state":{}
}
```