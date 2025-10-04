# Test Specification for Microsoft.Extensions.Logging.Redis

---

## Project Setup

### Technology Stack
- **Test Framework:** XUnit
- **Assertions:** Shouldly
- **Mocking:** NSubstitute (for unit tests only)
- **Docker Management:** DotNet.Testcontainers
- **Redis Client:** StackExchange.Redis

### Project Structure
```
Microsoft.Extensions.Logging.Redis.Tests/
├── Unit/
│   ├── RedisLoggerTests.cs
│   ├── RedisLoggerProviderTests.cs
│   └── ConfigurationTests.cs
├── Integration/
│   ├── BasicLoggingTests.cs
│   ├── ConnectionResilienceTests.cs
│   ├── PerformanceTests.cs
│   ├── EdgeCaseTests.cs
│   └── ConcurrencyTests.cs
├── Infrastructure/
│   ├── RedisIntegrationTestBase.cs
│   └── RedisTestHelpers.cs
└── TestData/
    ├── SampleMessages.cs
    └── SampleExceptions.cs
```

---

## Test Infrastructure Requirements

### Base Class: `RedisIntegrationTestBase`

**Framework Integration:**
- Implement `IAsyncLifetime` from XUnit
- Use `InitializeAsync()` for container setup
- Use `DisposeAsync()` for container cleanup

**Container Management:**
- Use DotNet.Testcontainers to create Redis container
- Configure with image: "redis:7-alpine"
- Map port 6379 to random available host port
- Implement wait strategy: poll Redis PING until successful (max 30 seconds, 1 second intervals)
- Enable automatic cleanup after tests

**Helper Methods Required:**
1. `GetConnectionString()` - Returns connection string from container
2. `GetRedisConnection()` - Returns IConnectionMultiplexer instance
3. `ClearRedisData(string? listKey = null)` - Clears specific key or entire database
4. `GetLogsFromRedis(string listKey)` - Retrieves all log entries from Redis list
5. `GetLogCount(string listKey)` - Returns count of logs in list
6. `WaitForLogCount(string listKey, int expectedCount, TimeSpan timeout)` - Polls until count matches or timeout

**Properties:**
- `RedisContainer` - IContainer instance
- `ConnectionString` - String containing host:port
- `DefaultListKey` - Constant "logs" for standard tests

---

## Test Categories and Specifications

### 1. UNIT TESTS (No Docker Required)

#### 1.1 RedisLoggerProvider Tests
Mock the Redis connection using NSubstitute

**Test: Constructor_WithNullConfiguration_ThrowsArgumentNullException**
- Create provider with null connection string
- Should throw ArgumentNullException

**Test: Constructor_WithEmptyListKey_ThrowsArgumentException**
- Create provider with empty or whitespace list key
- Should throw ArgumentException

**Test: CreateLogger_ReturnsSameInstanceForSameCategory**
- Call CreateLogger twice with same category name
- Both returns should reference same logger instance (or equal loggers)

**Test: Dispose_DisposesConnectionMultiplexer**
- Create provider, call Dispose
- Verify Redis connection is disposed
- Further logging attempts should not throw

#### 1.2 RedisLogger Tests (Mocked Redis)

**Test: Log_WithLogLevelNone_DoesNotWriteToRedis**
- Configure with LogLevel.None
- Call Log method
- Verify Redis was not called

**Test: IsEnabled_ReturnsTrueForEnabledLevels**
- Set minimum level to Information
- IsEnabled(LogLevel.Information) should return true
- IsEnabled(LogLevel.Debug) should return false

**Test: Log_WithNullFormatter_ThrowsArgumentNullException**
- Call Log with null formatter
- Should throw ArgumentNullException

---

### 2. INTEGRATION TESTS (Require Docker Redis)

All integration tests should inherit from `RedisIntegrationTestBase`

#### 2.1 Basic Logging Tests

**Test: LogInformation_WritesToRedisListWithCorrectKey**
- Setup: Configure logger with connection string and list key "test-logs"
- Action: Log single information message "Hello Redis"
- Assert: 
  - Redis list "test-logs" exists
  - List contains exactly 1 entry
  - Entry contains "Hello Redis"
  - Entry contains "Information" log level
  - Entry has valid timestamp

**Test: LogMultipleLevels_AllAppearInRedis**
- Action: Log messages at each level: Trace, Debug, Information, Warning, Error, Critical
- Assert:
  - List contains 6 entries
  - Each entry has correct log level indicator
  - Order matches logging sequence

**Test: LogWithCategory_IncludesCategoryInEntry**
- Setup: Create logger with category "MyApp.Services"
- Action: Log message
- Assert: Entry contains category name

**Test: LogWithEventId_IncludesEventIdInEntry**
- Action: Log with EventId(100, "UserAction")
- Assert: Entry contains event ID and name

#### 2.2 Structured Logging Tests

**Test: LogWithTemplateParameters_CapturesStructuredData**
- Action: Log "User {UserId} performed {Action}" with parameters UserId=123, Action="Login"
- Assert: 
  - Entry contains structured data
  - Can extract UserId value 123
  - Can extract Action value "Login"

**Test: LogWithScope_IncludesScopeData**
- Action: 
  - Begin scope with key-value pairs: RequestId="ABC123", UserId=456
  - Log message within scope
- Assert:
  - Entry contains scope data
  - RequestId and UserId are present

**Test: LogWithNestedScopes_IncludesAllScopes**
- Action:
  - Begin outer scope with CorrelationId
  - Begin inner scope with RequestId
  - Log message
- Assert:
  - Both CorrelationId and RequestId in entry
  - Scopes properly nested/formatted

#### 2.3 Exception Logging Tests

**Test: LogException_IncludesExceptionDetails**
- Action: 
  - Create Exception with message "Test error"
  - Log using LogError(ex, "An error occurred")
- Assert:
  - Entry contains exception message
  - Entry contains exception type name
  - Entry contains stack trace

**Test: LogExceptionWithInnerException_IncludesAllLevels**
- Action:
  - Create exception with 2 levels of inner exceptions
  - Log exception
- Assert:
  - All exception messages present
  - Inner exception details included

**Test: LogAggregateException_IncludesAllInnerExceptions**
- Action:
  - Create AggregateException with 3 inner exceptions
  - Log exception
- Assert:
  - All 3 inner exception messages present

#### 2.4 Configuration Tests

**Test: MultipleListKeys_LogsToCorrectLists**
- Setup:
  - Create first logger with key "logs-app1"
  - Create second logger with key "logs-app2"
- Action:
  - Log from first logger
  - Log from second logger
- Assert:
  - "logs-app1" contains only first message
  - "logs-app2" contains only second message

**Test: ConnectionStringWithPassword_ConnectsSuccessfully**
- Setup: Start Redis with password (use container command args)
- Action: Configure logger with password in connection string
- Assert: Logging works without errors

**Test: CustomConnectionOptions_Applied**
- Setup: Create ConfigurationOptions with specific settings
- Action: Pass options to provider
- Assert: Connection uses specified options

#### 2.5 Connection Resilience Tests

**Test: RedisUnavailableAtStartup_DoesNotThrowException**
- Setup: Do NOT start Redis container
- Action: Create logger and attempt to log
- Assert: 
  - No exceptions thrown
  - Application continues functioning

**Test: RedisDisconnectsDuringLogging_HandlesGracefully**
- Setup: Start Redis, create logger, log successfully
- Action:
  - Stop Redis container
  - Attempt to log multiple messages
- Assert:
  - No exceptions thrown
  - Application doesn't crash

**Test: RedisReconnects_LoggingResumes**
- Setup: Start Redis, create logger
- Action:
  - Stop Redis container
  - Wait 2 seconds
  - Restart Redis container (new instance with same port)
  - Log message
  - Wait up to 5 seconds
- Assert:
  - Eventually, new message appears in Redis
  - Logger auto-reconnects

**Test: ConnectionTimeout_HandledGracefully**
- Setup: Configure connection with very short timeout (100ms)
- Action: Simulate network delay or block connection
- Assert: Timeout doesn't crash application

#### 2.6 Performance Tests

**Test: Log10000Messages_CompletesWithinTimeLimit**
- Action: Log 10,000 sequential messages
- Assert:
  - All 10,000 messages in Redis
  - Completion time < 5 seconds
  - No errors

**Test: ConcurrentLogging_NoMessageLoss**
- Action:
  - Spawn 20 parallel tasks
  - Each task logs 500 messages (10,000 total)
  - Use unique message IDs
- Assert:
  - Redis contains exactly 10,000 entries
  - All unique message IDs present
  - No duplicates

**Test: LogLargeMessage_HandledCorrectly**
- Action: Log message with 1MB string content
- Assert:
  - Message written to Redis
  - Message not truncated
  - Can retrieve full content

**Test: HighFrequencyLogging_NoBackpressureIssues**
- Action: Log 100 messages per second for 10 seconds
- Assert:
  - All 1,000 messages in Redis
  - No memory buildup
  - Consistent throughput

#### 2.7 Edge Cases

**Test: LogNullMessage_HandledGracefully**
- Action: Call Log with null message
- Assert: No exception, entry written with null indicator

**Test: LogEmptyString_WrittenToRedis**
- Action: Log empty string ""
- Assert: Entry exists with empty message field

**Test: LogWhitespaceOnlyMessage_PreservedInRedis**
- Action: Log "   " (spaces only)
- Assert: Whitespace preserved in entry

**Test: LogUnicodeCharacters_EncodedCorrectly**
- Action: Log "Hello 世界 🌍 Привет"
- Assert:
  - All Unicode characters preserved
  - Emoji intact
  - No encoding corruption

**Test: LogSpecialCharacters_EscapedCorrectly**
- Action: Log message containing: \n, \r, \t, ", ', {, }, [, ]
- Assert: All special characters preserved and properly escaped

**Test: LogVeryLongCategoryName_Handled**
- Action: Create logger with 5000-character category name
- Assert: 
  - Logging succeeds
  - Category name present in entry

**Test: LogWithNullScopeValue_HandledGracefully**
- Action: Begin scope with null value, log message
- Assert: No exception, scope recorded appropriately

**Test: DisposedProvider_LoggingIgnored**
- Action:
  - Create provider, get logger
  - Dispose provider
  - Attempt to log
- Assert:
  - No exception thrown
  - No crash
  - Message may or may not be written (implementation dependent)

**Test: MultipleProviderDisposals_NoError**
- Action: Call Dispose on provider multiple times
- Assert: No exception, idempotent behavior

#### 2.8 Filtering Tests

**Test: MinimumLogLevel_FiltersLowerLevels**
- Setup: Configure minimum level as Warning
- Action: Log Debug, Information, Warning, Error
- Assert:
  - Only Warning and Error in Redis
  - Debug and Information not written

**Test: CategoryFilter_OnlyMatchingCategoriesLogged**
- Setup: Configure filter for category "MyApp.*"
- Action:
  - Log from "MyApp.Service" (should log)
  - Log from "OtherApp.Service" (should not log)
- Assert:
  - Only "MyApp.Service" logs in Redis

#### 2.9 Redis-Specific Tests

**Test: RedisListType_UsesLPUSH**
- Action: Log several messages
- Assert:
  - Redis key type is "list"
  - Messages ordered correctly (verify with LRANGE)
  - Newest or oldest first (depending on implementation)

**Test: MultipleLoggers_ShareSameConnection**
- Action:
  - Create multiple logger instances from same provider
  - Log from each
- Assert:
  - All use same connection multiplexer
  - Connection count doesn't grow unbounded

**Test: RedisListGrowsLarge_PerformanceRemainsSteady**
- Setup: Pre-populate Redis with 100,000 log entries
- Action: Log 1,000 more messages and measure time
- Assert: Performance similar to empty list scenario

#### 2.10 Memory and Resource Tests

**Test: LongRunningLogging_NoMemoryLeak**
- Action:
  - Log 100 messages per second
  - Run for 60 seconds
  - Monitor memory usage
- Assert:
  - Memory usage stable
  - No continuous growth
  - Memory released appropriately

**Test: Dispose_ReleasesResources**
- Action:
  - Create provider
  - Dispose provider
  - Check connection state
- Assert:
  - Redis connection closed
  - All resources released

#### 2.11 Format and Serialization Tests

**Test: LogEntry_IsValidJson**
- Action: Log message
- Retrieve entry from Redis
- Assert: Entry can be parsed as valid JSON

**Test: LogEntry_ContainsTimestamp**
- Action: Log message
- Assert:
  - Entry has timestamp field
  - Timestamp in ISO 8601 format
  - Timestamp approximately matches current time (within 1 second)

**Test: LogEntry_ContainsAllStandardFields**
- Action: Log message with all features (level, category, event ID, message, exception)
- Assert entry contains fields:
  - Timestamp
  - LogLevel
  - Category
  - EventId (name and id)
  - Message
  - Exception (if present)

**Test: DeserializedLogEntry_CanBeQueried**
- Action: Log structured message
- Retrieve and deserialize entry
- Assert: Can access properties programmatically

---

## Test Data Specifications

### Sample Messages
- Simple: "Hello World"
- With parameters: "User {userId} logged in"
- Long: 10KB string of repeated text
- Very long: 1MB string
- Unicode: "Hello 世界 🌍 مرحبا Привет"
- Special chars: "Line1\nLine2\tTabbed \"Quoted\""
- Empty: ""
- Whitespace: "   "
- Null: null

### Sample Exceptions
- ArgumentException with message
- NullReferenceException with stack trace
- InvalidOperationException with inner exception
- Custom exception with Data dictionary
- AggregateException with 3 inner exceptions

### Sample Structured Data
- UserId: 12345
- UserName: "john.doe"
- RequestId: "req-abc-123"
- CorrelationId: Guid
- IpAddress: "192.168.1.1"
- Complex object: { Name: "Test", Values: [1,2,3] }

---

## Assertion Patterns (Using Shouldly)

### General Pattern
```
// Count assertions
logCount.ShouldBe(expectedCount);
logs.ShouldNotBeEmpty();

// String assertions
logEntry.ShouldContain("expected text");
logEntry.ShouldNotBeNullOrWhiteSpace();
logLevel.ShouldBe("Information");

// Collection assertions
logs.Count.ShouldBe(5);
logs.ShouldContain(l => l.Contains("search text"));
logs.ShouldAllBe(l => l.Contains("common text"));

// Exception assertions
Should.Throw<ArgumentNullException>(() => CreateProvider(null));
Should.NotThrow(() => logger.LogInformation("test"));

// Timing assertions
elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));

// Numeric assertions
messageCount.ShouldBeGreaterThan(0);
messageCount.ShouldBeInRange(95, 105); // For concurrent tests with some variance
```

---

## Test Execution Guidelines

### Test Isolation
- Each test should use unique list key (e.g., include test method name)
- OR clear Redis data before each test
- Tests should be runnable in any order
- Tests should be runnable in parallel where possible

### Async/Await Usage
- All Redis operations should be async
- Use `await` properly in test methods
- XUnit tests should return `Task` for async tests

### Timeouts
- Integration tests timeout: 30 seconds each
- Performance tests timeout: 60 seconds
- Use `WaitForLogCount` helper with timeout instead of fixed delays

### Cleanup
- Base class handles container cleanup
- Individual tests should clean their specific list keys if needed
- Don't leave test data in Redis between test runs

---

## CI/CD Considerations

### Docker Requirements
- CI environment must have Docker available
- Tests should check for Docker availability and skip/warn if not present
- Use XUnit [Fact(Skip = "Requires Docker")] for conditional execution

### Parallel Execution
- Use collection fixtures for tests that can share container
- Use unique list keys to enable parallel test execution
- Avoid global state modifications

### Test Output
- Log container startup/shutdown events
- Output Redis connection string for debugging
- Log test timing information for performance tests

---

## Coverage Requirements

### Minimum Coverage Targets
- Line coverage: 90%
- Branch coverage: 85%
- Critical paths: 100% (logging, connection, disposal)

### What Must Be Tested
- All public methods
- All configuration options
- All error handling paths
- Connection lifecycle
- Concurrent scenarios
- Resource disposal

### What Can Be Skipped
- Private implementation details
- Third-party library behavior (StackExchange.Redis)
- Docker infrastructure code

---

## Success Criteria

A test implementation is complete when:
1. All specified test cases are implemented
2. All tests pass consistently (not flaky)
3. Tests can run in parallel
4. Docker container managed correctly (no orphans)
5. Code coverage meets targets
6. Tests complete in reasonable time (< 5 minutes total suite)
7. Edge cases properly handled
8. No memory leaks detected
9. Documentation exists for running tests
10. CI/CD pipeline successfully executes tests
