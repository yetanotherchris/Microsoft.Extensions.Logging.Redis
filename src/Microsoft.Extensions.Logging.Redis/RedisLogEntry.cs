using System;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.Logging.Redis;

internal sealed record RedisLogEntry(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("eventId")] int? EventId,
    [property: JsonPropertyName("eventName")] string? EventName,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("exception")] string? Exception,
    [property: JsonPropertyName("state")] object? State);
