# LlamaCpp Backend Testing Guide

This guide provides comprehensive testing procedures for the LlamaCpp backend integration.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Build and Verify](#build-and-verify)
3. [Unit Tests](#unit-tests)
4. [Integration Tests](#integration-tests)
5. [Manual Testing](#manual-testing)
6. [Test Scenarios](#test-scenarios)
7. [Troubleshooting Tests](#troubleshooting-tests)

## Prerequisites

### 1. Install .NET SDK

```bash
# Verify .NET is installed
dotnet --version

# Should be .NET 8.0 or later
```

### 2. Setup llama.cpp Server

```bash
# Clone and build llama.cpp
git clone https://github.com/ggerganov/llama.cpp.git
cd llama.cpp
make

# Start server on port 8080
./llama-server --port 8080 -c 4096
```

### 3. Download Test Model

For testing, use a small model like TinyLlama:

```bash
mkdir -p ./test-models

# Download TinyLlama (only ~600MB, quick to download)
wget https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf \
  -O ./test-models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf
```

## Build and Verify

### 1. Clean Build

```bash
cd /path/to/mostlyucid.llmbackend

# Clean previous builds
dotnet clean

# Restore packages
dotnet restore

# Build the project
dotnet build

# Look for successful build message
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)
```

### 2. Verify New Files

```bash
# Check that new files were added
ls -la Services/LlamaCppLlmBackend.cs
ls -la docs/LLAMACPP-INTEGRATION.md
ls -la examples/llamacpp-config.example.json

# Verify configuration enum was updated
grep -n "LlamaCpp" Configuration/LlmSettings.cs

# Should show LlamaCpp in the enum around line 510
```

### 3. Check for Compilation Errors

```bash
# Build in Release mode
dotnet build -c Release

# Should complete without errors
```

## Unit Tests

### Create Unit Test File

Create `Tests/LlamaCppLlmBackendTests.cs`:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;

namespace Mostlyucid.LlmBackend.Tests;

public class LlamaCppLlmBackendTests
{
    private readonly Mock<ILogger<LlamaCppLlmBackend>> _loggerMock;
    private readonly LlmBackendConfig _config;

    public LlamaCppLlmBackendTests()
    {
        _loggerMock = new Mock<ILogger<LlamaCppLlmBackend>>();
        _config = new LlmBackendConfig
        {
            Name = "Test-LlamaCpp",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = "http://localhost:8080",
            ModelName = "test-model",
            ModelPath = "./test-models/test.gguf",
            AutoDownloadModel = false, // Disable for unit tests
            ContextSize = 2048
        };
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        // Assert
        Assert.NotNull(backend);
        Assert.Equal("Test-LlamaCpp", backend.Name);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenServerRunning_ReturnsTrue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var result = await backend.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenServerNotRunning_ReturnsFalse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        // Act
        var result = await backend.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CompleteAsync_ShouldReturnSuccessResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("completion")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""content"": ""This is a test response"",
                    ""tokens_predicted"": 5,
                    ""tokens_evaluated"": 10
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new LlmRequest
        {
            Prompt = "Test prompt",
            MaxTokens = 100
        };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("This is a test response", response.Text);
        Assert.Equal(15, response.TotalTokens); // 5 + 10
    }

    [Fact]
    public async Task ChatAsync_ShouldReturnSuccessResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("v1/chat/completions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""choices"": [{
                        ""message"": {
                            ""content"": ""Chat response""
                        },
                        ""finish_reason"": ""stop""
                    }],
                    ""usage"": {
                        ""total_tokens"": 20,
                        ""prompt_tokens"": 12,
                        ""completion_tokens"": 8
                    }
                }")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };

        var backend = new LlamaCppLlmBackend(_config, _loggerMock.Object, httpClient);

        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Act
        var response = await backend.ChatAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Chat response", response.Text);
        Assert.Equal(20, response.TotalTokens);
        Assert.Equal("stop", response.FinishReason);
    }
}
```

### Run Unit Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test -v n

# Run specific test class
dotnet test --filter "FullyQualifiedName~LlamaCppLlmBackendTests"
```

## Integration Tests

### 1. Setup Integration Test

Create `Tests/LlamaCppIntegrationTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;

namespace Mostlyucid.LlmBackend.Tests.Integration;

public class LlamaCppIntegrationTests
{
    private const string ServerUrl = "http://localhost:8080";

    [Fact(Skip = "Requires llama.cpp server running")]
    public async Task RealServer_IsAvailable_ReturnsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        var serviceProvider = services.BuildServiceProvider();

        var config = new LlmBackendConfig
        {
            Name = "Integration-Test",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = ServerUrl,
            ModelPath = "./test-models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",
            AutoDownloadModel = false
        };

        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<LlamaCppLlmBackend>();
        var httpClient = factory.CreateClient();

        var backend = new LlamaCppLlmBackend(config, logger, httpClient);

        // Act
        var available = await backend.IsAvailableAsync();

        // Assert
        Assert.True(available, "LlamaCpp server should be available");
    }

    [Fact(Skip = "Requires llama.cpp server running with model loaded")]
    public async Task RealServer_CompleteAsync_ReturnsValidResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddHttpClient();
        var serviceProvider = services.BuildServiceProvider();

        var config = new LlmBackendConfig
        {
            Name = "Integration-Test",
            Type = LlmBackendType.LlamaCpp,
            BaseUrl = ServerUrl,
            ModelName = "tinyllama",
            ContextSize = 2048
        };

        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<LlamaCppLlmBackend>();
        var httpClient = factory.CreateClient();

        var backend = new LlamaCppLlmBackend(config, logger, httpClient);

        var request = new LlmRequest
        {
            Prompt = "What is 2+2?",
            MaxTokens = 50,
            Temperature = 0.7
        };

        // Act
        var response = await backend.CompleteAsync(request);

        // Assert
        Assert.True(response.Success, $"Request should succeed: {response.ErrorMessage}");
        Assert.NotEmpty(response.Text);
        Assert.True(response.DurationMs > 0);
        Console.WriteLine($"Response: {response.Text}");
    }
}
```

### 2. Run Integration Tests

```bash
# Start llama.cpp server first
./llama-server -m ./test-models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf --port 8080 -c 2048

# In another terminal, run integration tests
dotnet test --filter "Category=Integration"

# Or run without skip
dotnet test --filter "FullyQualifiedName~LlamaCppIntegrationTests"
```

## Manual Testing

### 1. Create Test Console App

Create `TestApp/Program.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.DependencyInjection;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
services.AddLlmBackend(configuration);

var serviceProvider = services.BuildServiceProvider();
var llmService = serviceProvider.GetRequiredService<ILlmService>();

Console.WriteLine("Testing LlamaCpp Backend...\n");

// Test 1: Completion
Console.WriteLine("Test 1: Completion");
var completionRequest = new LlmRequest
{
    Prompt = "Explain what an LLM is in one sentence.",
    MaxTokens = 50,
    Temperature = 0.7
};

var completionResponse = await llmService.CompleteAsync(completionRequest);
Console.WriteLine($"Success: {completionResponse.Success}");
Console.WriteLine($"Response: {completionResponse.Text}");
Console.WriteLine($"Duration: {completionResponse.DurationMs}ms");
Console.WriteLine($"Backend: {completionResponse.Backend}\n");

// Test 2: Chat
Console.WriteLine("Test 2: Chat");
var chatRequest = new ChatRequest
{
    Messages = new List<ChatMessage>
    {
        new() { Role = "system", Content = "You are a helpful assistant." },
        new() { Role = "user", Content = "What is 2+2?" }
    },
    Temperature = 0.7,
    MaxTokens = 30
};

var chatResponse = await llmService.ChatAsync(chatRequest);
Console.WriteLine($"Success: {chatResponse.Success}");
Console.WriteLine($"Response: {chatResponse.Text}");
Console.WriteLine($"Duration: {chatResponse.DurationMs}ms");

Console.WriteLine("\nTests completed!");
```

Create `TestApp/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Mostlyucid.LlmBackend": "Debug"
    }
  },
  "LlmSettings": {
    "SelectionStrategy": "Failover",
    "Backends": [
      {
        "Name": "LlamaCpp-Test",
        "Type": "LlamaCpp",
        "BaseUrl": "http://localhost:8080",
        "ModelName": "tinyllama-1.1b-chat",
        "ModelPath": "./test-models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",
        "ModelUrl": "https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",
        "AutoDownloadModel": true,
        "ContextSize": 2048,
        "Temperature": 0.7,
        "MaxOutputTokens": 500,
        "Priority": 1,
        "Enabled": true
      }
    ]
  }
}
```

### 2. Run Manual Tests

```bash
# Build test app
dotnet build TestApp

# Run test app
dotnet run --project TestApp

# Watch for log output including download progress
```

## Test Scenarios

### Scenario 1: Auto-Download Model

```bash
# 1. Ensure model doesn't exist
rm -f ./test-models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf

# 2. Start server
./llama-server --port 8080 -c 2048

# 3. Run test app with AutoDownloadModel = true
dotnet run --project TestApp

# 4. Verify logs show download progress
# Expected:
# [LlamaCpp-Test] Downloading model from https://...
# [LlamaCpp-Test] Download progress: 10.5% (...)
# [LlamaCpp-Test] Model downloaded successfully
```

### Scenario 2: Use Existing Model

```bash
# 1. Ensure model exists
ls -lh ./test-models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf

# 2. Set AutoDownloadModel = false in config

# 3. Run test
dotnet run --project TestApp

# 4. Verify no download occurs
# Expected:
# [LlamaCpp-Test] Model already exists at ...
```

### Scenario 3: GPU Acceleration

Update config:

```json
{
  "GpuLayers": 20,
  "ContextSize": 4096
}
```

Run and verify faster performance.

### Scenario 4: Failover

```json
{
  "SelectionStrategy": "Failover",
  "Backends": [
    {
      "Name": "Primary-Cloud",
      "Type": "OpenAI",
      "Priority": 1,
      "Enabled": false
    },
    {
      "Name": "Fallback-Local",
      "Type": "LlamaCpp",
      "Priority": 2,
      "Enabled": true
    }
  ]
}
```

Verify fallback to LlamaCpp when primary is disabled.

### Scenario 5: Large Context

```json
{
  "ContextSize": 8192,
  "MaxOutputTokens": 4000
}
```

Test with long prompt to verify context handling.

## Troubleshooting Tests

### Build Errors

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build -v detailed

# Check for missing references
grep -r "LlamaCpp" *.csproj
```

### Runtime Errors

```bash
# Enable detailed logging
export ASPNETCORE_ENVIRONMENT=Development

# Run with verbose output
dotnet run --project TestApp -v n
```

### Server Connection Issues

```bash
# Verify server is running
curl http://localhost:8080/health

# Check firewall
sudo netstat -tulpn | grep 8080

# Test with telnet
telnet localhost 8080
```

### Download Failures

```bash
# Test URL manually
wget -O /tmp/test.gguf https://huggingface.co/...

# Check disk space
df -h

# Verify permissions
ls -la ./test-models/
```

## Test Checklist

Before committing, verify:

- [ ] Project builds without errors: `dotnet build`
- [ ] No compilation warnings related to LlamaCpp
- [ ] Unit tests pass: `dotnet test`
- [ ] Integration tests pass (with server): `dotnet test --filter Integration`
- [ ] Manual test app runs successfully
- [ ] Auto-download works correctly
- [ ] Model reuse works (no re-download)
- [ ] Completion endpoint works
- [ ] Chat endpoint works
- [ ] Error handling works (server down)
- [ ] Logging outputs correctly
- [ ] Configuration validates properly
- [ ] Documentation is accurate
- [ ] Examples are up to date

## Performance Benchmarks

Expected performance for TinyLlama 1.1B Q4:

- **CPU Only**: 10-30 tokens/second
- **GPU (RTX 3090)**: 50-100+ tokens/second
- **First Request**: 2-5 seconds (model loading)
- **Subsequent**: < 1 second

Measure with:

```csharp
var sw = Stopwatch.StartNew();
var response = await llmService.CompleteAsync(request);
sw.Stop();
Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Tokens: {response.CompletionTokens}");
Console.WriteLine($"Speed: {response.CompletionTokens / (sw.ElapsedMilliseconds / 1000.0):F1} tokens/sec");
```

## Next Steps

After testing passes:

1. Update version number in `.csproj`
2. Update CHANGELOG.md
3. Create PR with test results
4. Tag release after merge

## Support

For testing issues:

1. Check llama.cpp server logs
2. Enable debug logging in LlmBackend
3. Review test logs in detail
4. Open issue with reproduction steps
