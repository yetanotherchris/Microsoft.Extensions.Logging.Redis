using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Logging.Redis.Tests.Infrastructure;

/// <summary>
/// Manages Docker Compose Redis container for integration tests.
/// Automatically starts Redis on creation and stops it on disposal.
/// </summary>
public class DockerComposeRedisManager : IDisposable, IAsyncDisposable
{
    private const string ComposeFile = "docker-compose.test.yml";
    private const string ServiceName = "redis-test";
    private const string ContainerName = "redis-integration-test";
    
    private bool _disposed;
    private bool _isStarted;

    public string ConnectionString => "localhost:6379";
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Creates and starts the Redis container
    /// </summary>
    public static async Task<DockerComposeRedisManager> CreateAsync(CancellationToken cancellationToken = default)
    {
        var manager = new DockerComposeRedisManager();
        await manager.StartAsync(cancellationToken);
        return manager;
    }

    /// <summary>
    /// Starts the Redis container and waits for it to be healthy
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isStarted)
        {
            return;
        }

        try
        {
            var projectRoot = FindProjectRoot();
            var composePath = Path.Combine(projectRoot, ComposeFile);
            
            if (!File.Exists(composePath))
            {
                Console.WriteLine($"❌ Docker Compose file not found at: {composePath}");
                IsAvailable = false;
                return;
            }

            Console.WriteLine("🔍 Starting Redis container for integration tests...");

            var composeCmd = GetComposeCommand();
            var startResult = await RunCommandAsync($"{composeCmd} -f \"{composePath}\" up -d {ServiceName}", cancellationToken);
            
            if (!startResult.Success)
            {
                Console.WriteLine($"❌ Failed to start Redis container: {startResult.Output}");
                IsAvailable = false;
                return;
            }

            Console.WriteLine("🔍 Waiting for Redis to be healthy...");
            
            // Wait for health check with timeout
            var isHealthy = await WaitForHealthyAsync(TimeSpan.FromSeconds(30), cancellationToken);
            
            if (isHealthy)
            {
                Console.WriteLine("✅ Redis is ready for integration tests!");
                Console.WriteLine($"🔗 Connection string: {ConnectionString}");
                _isStarted = true;
                IsAvailable = true;
            }
            else
            {
                Console.WriteLine("❌ Redis failed to become healthy within timeout");
                await ShowLogsAsync();
                IsAvailable = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error starting Redis: {ex.Message}");
            IsAvailable = false;
        }
    }

    /// <summary>
    /// Stops the Redis container
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isStarted)
        {
            return;
        }

        try
        {
            Console.WriteLine("🔍 Stopping Redis container...");

            var projectRoot = FindProjectRoot();
            var composePath = Path.Combine(projectRoot, ComposeFile);
            var composeCmd = GetComposeCommand();
            
            var result = await RunCommandAsync($"{composeCmd} -f \"{composePath}\" down", cancellationToken);

            if (result.Success)
            {
                Console.WriteLine("✅ Redis container stopped");
                _isStarted = false;
                IsAvailable = false;
            }
            else
            {
                Console.WriteLine($"❌ Failed to stop Redis container: {result.Output}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error stopping Redis: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows Redis container logs for debugging
    /// </summary>
    public async Task ShowLogsAsync(int lines = 20)
    {
        try
        {
            Console.WriteLine($"🔍 Redis logs (last {lines} lines):");
            var result = await RunCommandAsync($"docker logs {ContainerName} --tail {lines}");
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                Console.WriteLine(result.Output);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting logs: {ex.Message}");
        }
    }

    private async Task<bool> WaitForHealthyAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var attempt = 0;
        const int maxAttempts = 30;

        while (DateTime.UtcNow < deadline && attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await RunCommandAsync($"docker inspect {ContainerName} --format=\"{{{{.State.Health.Status}}}}\"", cancellationToken);
                
                if (result.Success && result.Output.Trim().Trim('"') == "healthy")
                {
                    return true;
                }

                // Also try a simple ping test as fallback
                var pingResult = await RunCommandAsync($"docker exec {ContainerName} redis-cli ping", cancellationToken);
                if (pingResult.Success && pingResult.Output.Trim() == "PONG")
                {
                    return true;
                }

                if (attempt % 5 == 0) // Show progress every 5 seconds
                {
                    Console.Write(".");
                }
                
                await Task.Delay(1000, cancellationToken);
                attempt++;
            }
            catch
            {
                // Continue trying
                await Task.Delay(1000, cancellationToken);
                attempt++;
            }
        }

        return false;
    }

    private static string GetComposeCommand()
    {
        // Try docker-compose first, then docker compose
        try
        {
            var result = RunCommand("docker-compose --version");
            if (result.Success)
            {
                return "docker-compose";
            }
        }
        catch
        {
            // Fall through to docker compose
        }

        return "docker compose";
    }

    private static string FindProjectRoot()
    {
        var current = Directory.GetCurrentDirectory();
        
        // Look for the docker-compose.test.yml file in the test directory
        while (current != null)
        {
            // Check if we're in or can find the test directory
            var testDir = Path.Combine(current, "tests", "Microsoft.Extensions.Logging.Redis.Tests");
            if (Directory.Exists(testDir) && File.Exists(Path.Combine(testDir, ComposeFile)))
            {
                return testDir;
            }
            
            // Check if current directory has the compose file (for when running from test directory)
            if (File.Exists(Path.Combine(current, ComposeFile)))
            {
                return current;
            }
            
            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        // Fallback to current directory
        return Directory.GetCurrentDirectory();
    }

    private static async Task<CommandResult> RunCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => RunCommand(command), cancellationToken);
    }

    private static CommandResult RunCommand(string command)
    {
        try
        {
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            
            var startInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            process.WaitForExit();

            var fullOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";
            
            return new CommandResult
            {
                Success = process.ExitCode == 0,
                Output = fullOutput.Trim(),
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                Output = ex.Message,
                ExitCode = -1
            };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Synchronous cleanup - best effort
            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning during dispose: {ex.Message}");
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await StopAsync();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private record CommandResult
    {
        public bool Success { get; init; }
        public string Output { get; init; } = string.Empty;
        public int ExitCode { get; init; }
    }
}