# LLMApi Integration Guide

This guide shows how to integrate the Mostlyucid.LlmBackend library into your LLMApi project.

## Table of Contents
- [Installation](#installation)
- [Configuration](#configuration)
- [Dependency Injection Setup](#dependency-injection-setup)
- [Usage Examples](#usage-examples)
- [Migration from Direct API Calls](#migration-from-direct-api-calls)
- [Advanced Features](#advanced-features)

## Installation

### Option 1: NuGet Package (When Published)
```bash
dotnet add package Mostlyucid.LlmBackend
```

### Option 2: Project Reference (For Now)
Add this to your LLMApi.csproj:
```xml
<ItemGroup>
  <ProjectReference Include="..\mostlyucid.llmbackend\Mostlyucid.LlmBackend.csproj" />
</ItemGroup>
```

## Configuration

### Basic Configuration (appsettings.json)

```json
{
  "LlmSettings": {
    "SelectionStrategy": "Failover",
    "TimeoutSeconds": 120,
    "MaxRetries": 3,
    "UseExponentialBackoff": true,
    "DefaultTemperature": 0.7,
    "DefaultMaxTokens": 2000,

    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 5,
      "DurationOfBreakSeconds": 30
    },

    "Caching": {
      "Enabled": true,
      "Provider": "Redis",
      "ExpirationMinutes": 60,
      "ConnectionString": "localhost:6379",
      "KeyPrefix": "llmapi:"
    },

    "Secrets": {
      "Provider": "EnvironmentVariables",
      "EnvironmentVariablePrefix": "LLMAPI_"
    },

    "Telemetry": {
      "EnableMetrics": true,
      "EnableTracing": true,
      "EnableCostTracking": true,
      "ServiceName": "LLMApi"
    },

    "Backends": [
      {
        "Name": "Primary-OpenAI",
        "Type": "OpenAI",
        "BaseUrl": "https://api.openai.com",
        "ApiKey": "${LLMAPI_OPENAI_APIKEY}",
        "ModelName": "gpt-4o",
        "Priority": 1,
        "Enabled": true,
        "EnableStreaming": true,
        "EnableFunctionCalling": true,
        "CostPerMillionInputTokens": 5.0,
        "CostPerMillionOutputTokens": 15.0
      },
      {
        "Name": "Fallback-Claude",
        "Type": "Anthropic",
        "BaseUrl": "https://api.anthropic.com",
        "ApiKey": "${LLMAPI_ANTHROPIC_APIKEY}",
        "ModelName": "claude-3-5-sonnet-20241022",
        "Priority": 2,
        "Enabled": true,
        "CostPerMillionInputTokens": 3.0,
        "CostPerMillionOutputTokens": 15.0
      },
      {
        "Name": "Azure-Backup",
        "Type": "AzureOpenAI",
        "BaseUrl": "https://your-resource.openai.azure.com",
        "ApiKey": "${LLMAPI_AZURE_APIKEY}",
        "DeploymentName": "gpt-4",
        "ApiVersion": "2024-02-15-preview",
        "Priority": 3,
        "Enabled": true
      },
      {
        "Name": "Local-Dev",
        "Type": "Ollama",
        "BaseUrl": "http://localhost:11434",
        "ModelName": "llama3",
        "Priority": 99,
        "Enabled": false
      }
    ]
  }
}
```

### Environment Variables
Set these in your deployment environment:
```bash
# For Linux/Mac
export LLMAPI_OPENAI_APIKEY="sk-..."
export LLMAPI_ANTHROPIC_APIKEY="sk-ant-..."
export LLMAPI_AZURE_APIKEY="your-azure-key"

# For Windows (PowerShell)
$env:LLMAPI_OPENAI_APIKEY="sk-..."
$env:LLMAPI_ANTHROPIC_APIKEY="sk-ant-..."
$env:LLMAPI_AZURE_APIKEY="your-azure-key"
```

## Dependency Injection Setup

### Program.cs / Startup.cs

```csharp
using Mostlyucid.LlmBackend.DependencyInjection;
using Mostlyucid.LlmBackend.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add LLM backend services
builder.Services.AddLlmBackend(builder.Configuration);

// Add your other services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Optional: Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<LlmBackendHealthCheck>("llm-backends");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

## Usage Examples

### Simple Completion Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

namespace LLMApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompletionController : ControllerBase
{
    private readonly ILlmService _llmService;
    private readonly ILogger<CompletionController> _logger;

    public CompletionController(
        ILlmService llmService,
        ILogger<CompletionController> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Complete([FromBody] CompletionRequest request)
    {
        try
        {
            var llmRequest = new LlmRequest
            {
                Prompt = request.Prompt,
                Temperature = request.Temperature ?? 0.7,
                MaxTokens = request.MaxTokens ?? 2000
            };

            var response = await _llmService.CompleteAsync(llmRequest);

            if (!response.Success)
            {
                return BadRequest(new
                {
                    error = "LLM request failed",
                    message = response.ErrorMessage
                });
            }

            return Ok(new
            {
                content = response.Content,
                backend = response.BackendUsed,
                model = response.ModelUsed,
                durationMs = response.DurationMs,
                tokens = new
                {
                    prompt = response.PromptTokens,
                    completion = response.CompletionTokens,
                    total = response.TotalTokens
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing completion request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public record CompletionRequest(
    string Prompt,
    double? Temperature = null,
    int? MaxTokens = null
);
```

### Chat Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

namespace LLMApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILlmService _llmService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ILlmService llmService,
        ILogger<ChatController> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequestDto request)
    {
        try
        {
            var messages = request.Messages.Select(m => new ChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            var chatRequest = new ChatRequest
            {
                Messages = messages,
                Temperature = request.Temperature ?? 0.7,
                MaxTokens = request.MaxTokens ?? 2000
            };

            var response = await _llmService.ChatAsync(chatRequest);

            if (!response.Success)
            {
                return BadRequest(new
                {
                    error = "Chat request failed",
                    message = response.ErrorMessage
                });
            }

            return Ok(new
            {
                message = new { role = "assistant", content = response.Content },
                backend = response.BackendUsed,
                model = response.ModelUsed,
                durationMs = response.DurationMs,
                tokens = new
                {
                    prompt = response.PromptTokens,
                    completion = response.CompletionTokens,
                    total = response.TotalTokens
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public record ChatRequestDto(
    List<MessageDto> Messages,
    double? Temperature = null,
    int? MaxTokens = null
);

public record MessageDto(string Role, string Content);
```

### Backend Management Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using Mostlyucid.LlmBackend.Interfaces;

namespace LLMApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackendsController : ControllerBase
{
    private readonly ILlmService _llmService;

    public BackendsController(ILlmService llmService)
    {
        _llmService = llmService;
    }

    [HttpGet]
    public IActionResult GetAvailableBackends()
    {
        var backends = _llmService.GetAvailableBackends();
        return Ok(new { backends });
    }

    [HttpGet("health")]
    public async Task<IActionResult> CheckHealth()
    {
        var health = await _llmService.TestBackendsAsync();
        var results = health.Select(kvp => new
        {
            name = kvp.Key,
            healthy = kvp.Value.IsHealthy,
            avgLatencyMs = kvp.Value.AverageLatencyMs,
            successfulRequests = kvp.Value.SuccessfulRequests,
            failedRequests = kvp.Value.FailedRequests,
            lastError = kvp.Value.LastError,
            lastSuccess = kvp.Value.LastSuccessfulRequest
        });

        return Ok(new { backends = results });
    }

    [HttpGet("statistics")]
    public IActionResult GetStatistics()
    {
        var stats = _llmService.GetStatistics();
        return Ok(new { statistics = stats });
    }
}
```

## Migration from Direct API Calls

### Before (Direct OpenAI Calls)

```csharp
// Old approach - tightly coupled to OpenAI
public class OldCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public async Task<string> GetCompletionAsync(string prompt)
    {
        var request = new
        {
            model = "gpt-4",
            messages = new[] { new { role = "user", content = prompt } }
        };

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/chat/completions",
            request);

        // Manual error handling, retry logic, etc.
        // No failover, no monitoring, no cost tracking
    }
}
```

### After (Using LlmBackend)

```csharp
// New approach - provider-agnostic with enterprise features
public class CompletionService
{
    private readonly ILlmService _llmService;

    public CompletionService(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<string> GetCompletionAsync(string prompt)
    {
        var request = new LlmRequest { Prompt = prompt };
        var response = await _llmService.CompleteAsync(request);

        // Automatic failover, retry, circuit breaker, caching
        // Cost tracking, monitoring, health checks - all built-in!

        return response.Content;
    }
}
```

## Advanced Features

### Using Specific Backends

```csharp
[HttpPost("claude")]
public async Task<IActionResult> UseClaudeSpecifically([FromBody] string prompt)
{
    var request = new LlmRequest
    {
        Prompt = prompt,
        BackendName = "Fallback-Claude"  // Target specific backend
    };

    var response = await _llmService.CompleteAsync(request);
    return Ok(response);
}
```

### Streaming Responses

```csharp
[HttpPost("stream")]
public async Task StreamCompletion([FromBody] string prompt)
{
    Response.ContentType = "text/event-stream";

    // TODO: Implement streaming when backend supports it
    // For now, use regular completion
    var request = new LlmRequest { Prompt = prompt };
    var response = await _llmService.CompleteAsync(request);

    await Response.WriteAsync($"data: {response.Content}\n\n");
    await Response.Body.FlushAsync();
}
```

### Using Prompt Builder

```csharp
public class AdvancedCompletionService
{
    private readonly ILlmService _llmService;
    private readonly IPromptBuilder _promptBuilder;

    public async Task<string> GetStructuredCompletionAsync(
        string userMessage,
        Dictionary<string, string> context)
    {
        var promptContext = new PromptContext
        {
            SystemMessage = "You are a helpful AI assistant for LLMApi",
            ContextVariables = context,
            MaxMemoryMessages = 10,
            IncludeHistory = true
        };

        var chatRequest = _promptBuilder.BuildChatRequest(
            userMessage,
            promptContext);

        var response = await _llmService.ChatAsync(chatRequest);
        return response.Content;
    }
}
```

### Cost Tracking

```csharp
[HttpGet("costs")]
public IActionResult GetCosts([FromQuery] DateTime? since = null)
{
    var stats = _llmService.GetStatistics();

    var costs = stats.Select(s => new
    {
        backend = s.Name,
        totalRequests = s.TotalRequests,
        // Calculate estimated costs based on token usage
        // This would require storing token counts per request
    });

    return Ok(new { costs });
}
```

### Middleware for Request Logging

```csharp
public class LlmRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LlmRequestLoggingMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context, ILlmService llmService)
    {
        var path = context.Request.Path.Value;
        if (path?.StartsWith("/api/") == true)
        {
            var stats = llmService.GetStatistics();
            context.Response.Headers.Add("X-LLM-Backend-Count",
                stats.Count.ToString());
        }

        await _next(context);
    }
}
```

## Best Practices for LLMApi

1. **Use Failover Strategy** - Configure multiple backends for reliability
2. **Enable Caching** - Reduce costs for repeated queries
3. **Monitor Statistics** - Track usage and optimize
4. **Use Environment Variables** - Keep API keys out of code
5. **Enable Circuit Breakers** - Prevent cascading failures
6. **Implement Rate Limiting** - Protect against abuse
7. **Add Health Checks** - Monitor backend availability
8. **Enable Cost Tracking** - Monitor and control expenses
9. **Use Structured Logging** - Track requests and responses
10. **Implement Request Validation** - Validate inputs before calling LLM

## Testing

### Unit Tests

```csharp
using Moq;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;
using Xunit;

public class CompletionControllerTests
{
    [Fact]
    public async Task Complete_ReturnsSuccessResponse()
    {
        // Arrange
        var mockLlmService = new Mock<ILlmService>();
        mockLlmService
            .Setup(s => s.CompleteAsync(It.IsAny<LlmRequest>(), default))
            .ReturnsAsync(new LlmResponse
            {
                Content = "Test response",
                Success = true,
                BackendUsed = "Test",
                DurationMs = 100
            });

        var controller = new CompletionController(
            mockLlmService.Object,
            Mock.Of<ILogger<CompletionController>>());

        // Act
        var result = await controller.Complete(
            new CompletionRequest("Test prompt"));

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
```

## Troubleshooting

### Issue: No backends available
**Solution**: Check configuration and ensure at least one backend is enabled

### Issue: All backends failing
**Solution**: Check health endpoint and backend API keys

### Issue: High latency
**Solution**: Enable caching and check backend response times

### Issue: Rate limiting errors
**Solution**: Adjust rate limit configuration or add more backends

## Next Steps

1. Set up your configuration in appsettings.json
2. Configure environment variables for API keys
3. Update your controllers to use ILlmService
4. Test with multiple backends
5. Monitor statistics and health
6. Enable caching and cost tracking
7. Deploy with confidence!
