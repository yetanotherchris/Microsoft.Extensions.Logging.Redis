# Microsoft.Extensions.Logging.Redis
A Redis `ILogger` provider that writes log events to a Redis list using [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/).

## Usage

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddRedis("localhost:6379", "logs");
    });
```
