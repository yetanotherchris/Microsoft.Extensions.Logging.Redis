# Microsoft.Extensions.Logging.Redis

[![NuGet](https://img.shields.io/nuget/v/redis-ilogger.svg)](https://www.nuget.org/packages/redis-ilogger/)

A Redis `ILogger` provider that writes log events to a Redis list using [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/).

## Usage

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddRedis("localhost:6379", "my-list-key");
    });
```


## Notes 
The library was written using Claude and Codex, and `serilog-sinks-redis` used as the reference implementation.