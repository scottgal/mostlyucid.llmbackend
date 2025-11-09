# Mostlyucid.LlmBackend

A robust, production-ready abstraction library for multiple LLM backends with enterprise-grade features including failover, load balancing, retry logic, circuit breakers, and comprehensive configuration options.

[![NuGet](https://img.shields.io/nuget/v/Mostlyucid.LlmBackend.svg)](https://www.nuget.org/packages/Mostlyucid.LlmBackend/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

### Supported LLM Providers
- **OpenAI** - GPT-3.5, GPT-4, GPT-4 Turbo, GPT-4o
- **Azure OpenAI** - Enterprise OpenAI deployments
- **Anthropic Claude** - Claude 3 (Opus, Sonnet, Haiku), Claude 3.5
- **Google Gemini** - Gemini Pro, Gemini Ultra (AI Studio and Vertex AI)
- **Cohere** - Command, Command R, Command R+
- **Ollama** - Local open-source models
- **LM Studio** - Local model serving
- **EasyNMT** - Translation-optimized models
- **Generic OpenAI-compatible** - Any OpenAI-compatible API

### Enterprise Features
- **Multiple Selection Strategies**
  - Failover (priority-based fallback)
  - Round Robin (load balancing)
  - Lowest Latency (performance-based routing)
  - Specific backend targeting
  - Random distribution

- **Resilience & Reliability**
  - Automatic retry with exponential backoff
  - Circuit breaker pattern
  - Health checks and monitoring
  - Request timeout management
  - Backend availability tracking

- **Security**
  - Multiple secrets providers (Azure Key Vault, AWS Secrets Manager, environment variables)
  - Managed identity support
  - Secure API key management

- **Performance**
  - Response caching (Memory, Redis, SQL Server)
  - Rate limiting
  - Connection pooling
  - Request queuing

- **Observability**
  - OpenTelemetry metrics and tracing
  - Comprehensive logging
  - Cost tracking
  - Performance statistics
  - Health status monitoring

- **Flexibility**
  - Pluggable prompt builders
  - Context memory with multiple providers
  - Streaming support
  - Function calling/tools (OpenAI, Anthropic)
  - Embeddings generation

### 🔌 Plugin Architecture

The library supports a powerful plugin system that allows you to add custom LLM providers without modifying the core library:

- **Drop-in DLL Plugins** - Simply place plugin DLLs in the `plugins` directory
- **Custom Providers** - Add support for any LLM provider via plugins
- **Full Feature Support** - Plugins get automatic failover, retries, caching, monitoring
- **NuGet Distribution** - Distribute plugins as NuGet packages
- **Hot Loading** - Load plugins at startup or dynamically at runtime

**Example**: Create a plugin for any LLM provider:
```csharp
public class MyCustomPlugin : ILlmBackendPlugin
{
    public string PluginId => "com.mycompany.customprovider";
    public IEnumerable<string> SupportedBackendTypes => new[] { "MyProvider" };
    // ... implement plugin interface
}
```

See [Plugin Development Guide](docs/PLUGIN-DEVELOPMENT.md) for complete documentation.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
```

## 🚀 Quick Start - Minimal Setup

### Option 1: Zero Configuration (Code-Only)

Get started in seconds with no config file required:

```csharp
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.DependencyInjection;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

// In Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register with inline configuration
builder.Services.AddLlmBackend(settings =>
{
    settings.Backends = new List<LlmBackendConfig>
    {
        new()
        {
            Name = "OpenAI",
            Type = LlmBackendType.OpenAI,
            BaseUrl = "https://api.openai.com",
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            ModelName = "gpt-4o",
            Enabled = true
        }
    };
});

var app = builder.Build();

// Use it!
app.MapGet("/ask", async (ILlmService llm, string question) =>
{
    var response = await llm.CompleteAsync(new LlmRequest { Prompt = question });
    return response.Content;
});

app.Run();
```

Set your API key:
```bash
export OPENAI_API_KEY=sk-your-key-here
dotnet run
```

Test it:
```bash
curl "http://localhost:5000/ask?question=What+is+2+2"
```

### Option 2: Absolute Minimum Config File

**appsettings.json** (just 5 lines of configuration):
```json
{
  "LlmSettings": {
    "Backends": [
      {
        "Name": "OpenAI",
        "Type": "OpenAI",
        "ApiKey": "sk-your-key-here",
        "ModelName": "gpt-4o"
      }
    ]
  }
}
```

**Program.cs**:
```csharp
builder.Services.AddLlmBackend(builder.Configuration);

app.MapGet("/ask", async (ILlmService llm, string question) =>
{
    var response = await llm.CompleteAsync(new LlmRequest { Prompt = question });
    return response.Content;
});
```

That's it! The library uses sensible defaults for everything else:
- ✅ BaseUrl auto-set to `https://api.openai.com`
- ✅ Temperature defaults to 0.7
- ✅ Max tokens defaults to 2000
- ✅ Automatic retry with exponential backoff
- ✅ 120-second timeout
- ✅ Circuit breaker enabled

### Option 3: With Local Ollama (Completely Free!)

No API key needed for local models:

**appsettings.json**:
```json
{
  "LlmSettings": {
    "Backends": [
      {
        "Name": "Ollama",
        "Type": "Ollama",
        "BaseUrl": "http://localhost:11434",
        "ModelName": "llama3"
      }
    ]
  }
}
```

**Setup**:
```bash
# Install Ollama
curl https://ollama.ai/install.sh | sh

# Pull a model
ollama pull llama3

# Start using it!
dotnet run
curl "http://localhost:5000/ask?question=Hello"
```

### Full Example with Chat

```csharp
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;

public class MyService
{
    private readonly ILlmService _llmService;

    public MyService(ILlmService llmService)
    {
        _llmService = llmService;
    }

    // Simple completion
    public async Task<string> AskQuestionAsync(string question)
    {
        var request = new LlmRequest
        {
            Prompt = question,
            Temperature = 0.7,
            MaxTokens = 500
        };

        var response = await _llmService.CompleteAsync(request);
        return response.Content;
    }

    // Chat with conversation history
    public async Task<string> ChatAsync(List<ChatMessage> messages)
    {
        var request = new ChatRequest
        {
            Messages = messages,
            Temperature = 0.7
        };

        var response = await _llmService.ChatAsync(request);
        return response.Content;
    }
}
```

**Usage**:
```csharp
// Inject the service
public class HomeController
{
    private readonly MyService _myService;

    public HomeController(MyService myService) => _myService = myService;

    // Simple question
    var answer = await _myService.AskQuestionAsync("What is the meaning of life?");

    // Chat conversation
    var messages = new List<ChatMessage>
    {
        new() { Role = "system", Content = "You are a helpful assistant" },
        new() { Role = "user", Content = "What is the capital of France?" }
    };
    var response = await _myService.ChatAsync(messages);
}
```

### Next Steps

- 📚 **Add failover**: Configure multiple backends for reliability
- 🔒 **Secure your keys**: Use environment variables or Azure Key Vault
- 📊 **Monitor usage**: Enable Prometheus metrics (see [Metrics Guide](docs/METRICS-AND-MONITORING.md))
- 🧪 **Add tests**: Use the testing fakes (see [Testing Guide](docs/TESTING-GUIDE.md))
- ⚡ **Optimize costs**: Enable caching and configure cheaper backup models

See [Advanced Configuration](#advanced-configuration) below for all available options.

## Advanced Configuration

### Complete Configuration Example

```json
{
  "LlmSettings": {
    "SelectionStrategy": "Failover",
    "TimeoutSeconds": 120,
    "MaxRetries": 3,
    "UseExponentialBackoff": true,
    "RetryDelayMs": 1000,
    "DefaultTemperature": 0.7,
    "DefaultMaxTokens": 2000,

    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 5,
      "DurationOfBreakSeconds": 30,
      "SamplingDurationSeconds": 60,
      "MinimumThroughput": 10
    },

    "RateLimit": {
      "Enabled": true,
      "MaxRequests": 100,
      "WindowSeconds": 60,
      "MaxConcurrentRequests": 10,
      "QueueLimit": 100
    },

    "Caching": {
      "Enabled": true,
      "Provider": "Redis",
      "ExpirationMinutes": 60,
      "MaxCacheSizeMb": 100,
      "ConnectionString": "localhost:6379",
      "KeyPrefix": "llm:"
    },

    "HealthCheck": {
      "Enabled": true,
      "IntervalSeconds": 60,
      "TimeoutSeconds": 10,
      "UnhealthyThreshold": 3,
      "HealthyThreshold": 2
    },

    "Secrets": {
      "Provider": "AzureKeyVault",
      "KeyVaultUrl": "https://your-vault.vault.azure.net/",
      "UseManagedIdentity": true
    },

    "Telemetry": {
      "EnableMetrics": true,
      "EnableTracing": true,
      "EnableDetailedLogging": false,
      "LogContent": false,
      "ServiceName": "MyApp",
      "EnableCostTracking": true
    },

    "Memory": {
      "Provider": "Redis",
      "ConnectionString": "localhost:6379",
      "DefaultTokenLimit": 4096,
      "EnableCompression": false,
      "TtlMinutes": 60
    },

    "Plugins": {
      "Enabled": true,
      "PluginDirectory": "plugins",
      "SearchSubdirectories": true,
      "LoadOnStartup": true
    },

    "Backends": [
      {
        "Name": "OpenAI-GPT4",
        "Type": "OpenAI",
        "BaseUrl": "https://api.openai.com",
        "ApiKey": "sk-...",
        "ModelName": "gpt-4o",
        "Temperature": 0.7,
        "MaxInputTokens": 128000,
        "MaxOutputTokens": 4096,
        "Priority": 1,
        "Enabled": true,
        "EnableStreaming": true,
        "EnableFunctionCalling": true,
        "CostPerMillionInputTokens": 5.0,
        "CostPerMillionOutputTokens": 15.0
      },
      {
        "Name": "Anthropic-Claude",
        "Type": "Anthropic",
        "BaseUrl": "https://api.anthropic.com",
        "ApiKey": "sk-ant-...",
        "ModelName": "claude-3-5-sonnet-20241022",
        "AnthropicVersion": "2023-06-01",
        "MaxInputTokens": 200000,
        "MaxOutputTokens": 4096,
        "Priority": 2,
        "Enabled": true,
        "CostPerMillionInputTokens": 3.0,
        "CostPerMillionOutputTokens": 15.0
      },
      {
        "Name": "Azure-GPT4",
        "Type": "AzureOpenAI",
        "BaseUrl": "https://your-resource.openai.azure.com",
        "ApiKey": "your-azure-key",
        "DeploymentName": "gpt-4",
        "ApiVersion": "2024-02-15-preview",
        "Priority": 3,
        "Enabled": true
      },
      {
        "Name": "Gemini-Pro",
        "Type": "Gemini",
        "BaseUrl": "https://generativelanguage.googleapis.com",
        "ApiKey": "your-google-api-key",
        "ModelName": "gemini-1.5-pro",
        "Priority": 4,
        "Enabled": true
      },
      {
        "Name": "Local-Ollama",
        "Type": "Ollama",
        "BaseUrl": "http://localhost:11434",
        "ModelName": "llama3",
        "Priority": 99,
        "Enabled": true
      }
    ]
  }
}
```

## Backend-Specific Configuration

### OpenAI

```json
{
  "Name": "OpenAI",
  "Type": "OpenAI",
  "BaseUrl": "https://api.openai.com",
  "ApiKey": "sk-...",
  "ModelName": "gpt-4o",
  "OrganizationId": "org-...",
  "Temperature": 0.7,
  "MaxOutputTokens": 4096,
  "TopP": 1.0,
  "FrequencyPenalty": 0.0,
  "PresencePenalty": 0.0,
  "StopSequences": ["END"],
  "EnableStreaming": true,
  "EnableFunctionCalling": true
}
```

### Anthropic Claude

```json
{
  "Name": "Claude",
  "Type": "Anthropic",
  "BaseUrl": "https://api.anthropic.com",
  "ApiKey": "sk-ant-...",
  "ModelName": "claude-3-5-sonnet-20241022",
  "AnthropicVersion": "2023-06-01",
  "MaxInputTokens": 200000,
  "MaxOutputTokens": 4096
}
```

### Azure OpenAI

```json
{
  "Name": "Azure",
  "Type": "AzureOpenAI",
  "BaseUrl": "https://your-resource.openai.azure.com",
  "ApiKey": "your-key",
  "DeploymentName": "gpt-4",
  "ApiVersion": "2024-02-15-preview"
}
```

### Google Gemini (AI Studio)

```json
{
  "Name": "Gemini",
  "Type": "Gemini",
  "BaseUrl": "https://generativelanguage.googleapis.com",
  "ApiKey": "your-api-key",
  "ModelName": "gemini-1.5-pro"
}
```

### Google Gemini (Vertex AI)

```json
{
  "Name": "Gemini-Vertex",
  "Type": "Gemini",
  "BaseUrl": "https://your-region-aiplatform.googleapis.com",
  "ProjectId": "your-project-id",
  "Location": "us-central1",
  "ModelName": "gemini-1.5-pro"
}
```

### Cohere

```json
{
  "Name": "Cohere",
  "Type": "Cohere",
  "BaseUrl": "https://api.cohere.ai",
  "ApiKey": "your-api-key",
  "ModelName": "command-r-plus"
}
```

### Ollama (Local)

```json
{
  "Name": "Ollama",
  "Type": "Ollama",
  "BaseUrl": "http://localhost:11434",
  "ModelName": "llama3"
}
```

## Selection Strategies

### Failover Strategy

Tries backends in priority order (lowest priority number first) until one succeeds.

```json
{
  "SelectionStrategy": "Failover",
  "Backends": [
    { "Name": "Primary", "Priority": 1 },
    { "Name": "Backup", "Priority": 2 },
    { "Name": "LastResort", "Priority": 3 }
  ]
}
```

### Round Robin Strategy

Distributes requests evenly across all enabled backends.

```json
{
  "SelectionStrategy": "RoundRobin"
}
```

### Lowest Latency Strategy

Routes requests to the backend with the best average response time.

```json
{
  "SelectionStrategy": "LowestLatency"
}
```

### Specific Backend

Target a specific named backend.

```csharp
var request = new LlmRequest
{
    Prompt = "Hello",
    BackendName = "Claude"  // Use specific backend
};
```

### Using Plugin Backends

Configure and use backends provided by plugins:

```json
{
  "Plugins": {
    "Enabled": true,
    "PluginDirectory": "plugins"
  },
  "Backends": [
    {
      "Name": "Mistral-Large",
      "Type": "OpenAI",
      "CustomBackendType": "Mistral",
      "BaseUrl": "https://api.mistral.ai",
      "ApiKey": "${MISTRAL_API_KEY}",
      "ModelName": "mistral-large-latest",
      "Priority": 1,
      "Enabled": true
    }
  ]
}
```

The `CustomBackendType` field tells the library to use a plugin-provided backend. The plugin DLL should be in the `plugins` directory.

## Usage Examples

### Simple Completion

```csharp
var response = await _llmService.CompleteAsync(new LlmRequest
{
    Prompt = "Explain quantum computing in simple terms",
    MaxTokens = 500
});

Console.WriteLine(response.Content);
```

### Chat Conversation

```csharp
var messages = new List<ChatMessage>
{
    new() { Role = "system", Content = "You are a helpful assistant" },
    new() { Role = "user", Content = "What is the capital of France?" },
    new() { Role = "assistant", Content = "The capital of France is Paris." },
    new() { Role = "user", Content = "What's its population?" }
};

var response = await _llmService.ChatAsync(new ChatRequest
{
    Messages = messages,
    Temperature = 0.7
});

Console.WriteLine(response.Content);
```

### Using Prompt Builder

```csharp
public class MyService
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly ILlmService _llmService;

    public async Task<string> TranslateAsync(string text, string targetLanguage)
    {
        _promptBuilder.AddToMemory("system", "You are a professional translator");

        var context = new PromptContext
        {
            SystemMessage = "Translate accurately, preserving formatting",
            ContextVariables = new Dictionary<string, string>
            {
                ["targetLanguage"] = targetLanguage,
                ["text"] = text
            }
        };

        var chatRequest = _promptBuilder.BuildChatRequest(
            $"Translate to {targetLanguage}",
            context
        );

        var response = await _llmService.ChatAsync(chatRequest);
        return response.Content;
    }
}
```

### Health Checking

```csharp
// Check all backends
var backends = await _llmService.TestBackendsAsync();
foreach (var (name, health) in backends)
{
    Console.WriteLine($"{name}: {(health.IsHealthy ? "Healthy" : "Unhealthy")}");
    Console.WriteLine($"  Avg Latency: {health.AverageLatencyMs}ms");
    Console.WriteLine($"  Success: {health.SuccessfulRequests}");
    Console.WriteLine($"  Failures: {health.FailedRequests}");
}
```

### Statistics Monitoring

```csharp
var stats = _llmService.GetStatistics();
foreach (var stat in stats)
{
    Console.WriteLine($"{stat.Name}:");
    Console.WriteLine($"  Total: {stat.TotalRequests}");
    Console.WriteLine($"  Success Rate: {(double)stat.SuccessfulRequests / stat.TotalRequests:P}");
    Console.WriteLine($"  Avg Response: {stat.AverageResponseTimeMs}ms");
    Console.WriteLine($"  Last Used: {stat.LastUsed}");
}
```

## Secrets Management

### Environment Variables

```json
{
  "Secrets": {
    "Provider": "EnvironmentVariables",
    "EnvironmentVariablePrefix": "LLM_"
  }
}
```

Then set: `LLM_OPENAI_APIKEY=sk-...`

### Azure Key Vault

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "KeyVaultUrl": "https://your-vault.vault.azure.net/",
    "UseManagedIdentity": true
  }
}
```

### AWS Secrets Manager

```json
{
  "Secrets": {
    "Provider": "AwsSecretsManager",
    "AwsRegion": "us-east-1",
    "UseManagedIdentity": true
  }
}
```

## Cost Tracking

Enable cost tracking to monitor API usage costs:

```json
{
  "Telemetry": {
    "EnableCostTracking": true
  },
  "Backends": [
    {
      "Name": "GPT-4",
      "CostPerMillionInputTokens": 5.0,
      "CostPerMillionOutputTokens": 15.0
    }
  ]
}
```

Access cost data from response:

```csharp
var response = await _llmService.CompleteAsync(request);
var estimatedCost = (response.PromptTokens / 1_000_000.0 * 5.0) +
                    (response.CompletionTokens / 1_000_000.0 * 15.0);
```

## Best Practices

1. **Use Failover Strategy** for production with multiple backends
2. **Enable Circuit Breakers** to prevent cascading failures
3. **Configure Health Checks** to automatically detect and skip unhealthy backends
4. **Use Secrets Management** (never commit API keys to source control)
5. **Enable Caching** for repeated queries to reduce costs
6. **Monitor Statistics** to optimize backend selection and performance
7. **Set Timeouts** appropriate for your use case
8. **Configure Rate Limits** to stay within API quotas
9. **Enable Telemetry** for production observability
10. **Use Cost Tracking** to monitor and optimize expenses

## Testing

### Unit Tests

```bash
dotnet test tests/Mostlyucid.LlmBackend.Tests
```

### Integration Tests

```bash
# Requires real API keys set in environment variables
dotnet test tests/Mostlyucid.LlmBackend.IntegrationTests
```

## Documentation

### Integration Guides
- **[LLMApi Integration Guide](docs/INTEGRATION-LLMAPI.md)** - Complete guide for integrating with LLMApi projects
- **[ResXTranslator Integration Guide](docs/INTEGRATION-RESXTRANSLATOR.md)** - Migration guide for ResXTranslator projects
- **[Plugin Development Guide](docs/PLUGIN-DEVELOPMENT.md)** - How to create custom LLM backend plugins

### Configuration Examples
- **[Complete Configuration Example](examples/appsettings.example.json)** - Fully commented configuration template

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Plugin Contributions

We especially welcome plugin contributions for new LLM providers! See the [Plugin Development Guide](docs/PLUGIN-DEVELOPMENT.md) to get started.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/scottgal/mostlyucid.llmbackend/issues)
- **Documentation**: [GitHub Wiki](https://github.com/scottgal/mostlyucid.llmbackend/wiki)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release history and updates.
