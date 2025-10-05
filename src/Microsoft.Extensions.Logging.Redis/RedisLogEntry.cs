using System.Text.Json.Serialization;

namespace Microsoft.Extensions.Logging.Redis;

/// <summary>
/// Represents a log entry that is serialized and sent to Redis.
/// </summary>
/// <param name="Timestamp">The timestamp of the log entry.</param>
/// <param name="Level">The log level (e.g., Information, Warning, Error).</param>
/// <param name="Category">The category of the log entry.</param>
/// <param name="EventId">The ID of the event associated with the log entry, if any.</param>
/// <param name="EventName">The name of the event associated with the log entry, if any.</param>
/// <param name="Message">The log message.</param>
/// <param name="Exception">The exception details, if any.</param>
/// <param name="State">The state object associated with the log entry, if any.</param>
public sealed record RedisLogEntry(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("eventId")] int? EventId,
    [property: JsonPropertyName("eventName")] string? EventName,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("exception")] string? Exception,
    [property: JsonPropertyName("state")] object? State);
