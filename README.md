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
  - **Simultaneous** (NEW! - parallel multi-LLM responses for creative tasks)

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
  - Cost tracking with **automatic budget limits** (NEW!)
  - Performance statistics
  - Health status monitoring
  - Prometheus/Grafana metrics support

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
    PreferredBackend = "Claude"  // Use specific backend
};
```

### Simultaneous Strategy (NEW!)

Call **multiple LLMs in parallel** and return all responses for comparison. Perfect for creative tasks where you want to see different perspectives!

```json
{
  "SelectionStrategy": "Simultaneous",
  "Backends": [
    { "Name": "Claude", "Type": "Anthropic", "ModelName": "claude-3-5-sonnet-20241022" },
    { "Name": "GPT4o", "Type": "OpenAI", "ModelName": "gpt-4o" },
    { "Name": "Llama3", "Type": "Ollama", "ModelName": "llama3" }
  ]
}
```

**Usage**:
```csharp
var response = await _llmService.CompleteAsync(new LlmRequest
{
    Prompt = "Write a creative opening for a sci-fi story"
});

// Primary response (first successful)
Console.WriteLine($"Primary ({response.Backend}): {response.Content}");

// Alternative responses from other LLMs
foreach (var alt in response.AlternativeResponses ?? new())
{
    Console.WriteLine($"\nAlternative ({alt.Backend}): {alt.Content}");
}
```

**Use Cases**:
- Creative writing (compare Claude's prose vs GPT-4's structure)
- A/B testing different model outputs
- Educational comparisons of LLM capabilities
- Building consensus from multiple models
- Quality assurance (pick the best response)

**Note**: This strategy calls ALL enabled backends in parallel, consuming quota/budget from each. Set appropriate `MaxSpendUsd` limits to control costs.

See the [SciFi Story Writer example](#example-3-collaborative-scifi-story-writer) for a complete implementation.

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

## Cost Tracking & Budget Limits (NEW!)

### Basic Cost Tracking

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

### Automatic Budget Limits (NEW!)

Set spending limits and **automatically disable backends** when exceeded:

```json
{
  "Backends": [
    {
      "Name": "GPT-4o",
      "Type": "OpenAI",
      "ModelName": "gpt-4o",
      "CostPerMillionInputTokens": 2.50,
      "CostPerMillionOutputTokens": 10.00,
      "MaxSpendUsd": 10.00,
      "SpendResetPeriod": "Daily",
      "LogBudgetExceeded": true
    }
  ]
}
```

**Configuration Options**:
- `MaxSpendUsd`: Maximum spend limit in USD (null = unlimited)
- `SpendResetPeriod`: When to reset the counter
  - `Daily` - Reset at midnight UTC
  - `Weekly` - Reset on configured day (default: Monday)
  - `Monthly` - Reset on configured day (default: 1st)
  - `Never` - Manual reset only
- `SpendResetDayOfWeek`: For weekly resets (0-6, Sunday-Saturday)
- `SpendResetDayOfMonth`: For monthly resets (1-31)
- `LogBudgetExceeded`: Whether to log warnings when budget exceeded

**How it Works**:
1. Each request's cost is tracked automatically
2. When `MaxSpendUsd` is exceeded, backend becomes unavailable
3. Backend is automatically re-enabled at next reset period
4. Failover strategy will use next available backend
5. Prometheus metrics track current spend vs budget

**Prometheus Metrics**:
```
llm_backend_budget_usd{backend="GPT-4o",limit_type="current"} 7.32
llm_backend_budget_usd{backend="GPT-4o",limit_type="max"} 10.00
```

**Example: Tiered Failover with Budgets**
```json
{
  "SelectionStrategy": "Failover",
  "Backends": [
    {
      "Name": "GPT-4o-Primary",
      "Priority": 1,
      "MaxSpendUsd": 50.00,
      "SpendResetPeriod": "Daily"
    },
    {
      "Name": "Claude-Backup",
      "Priority": 2,
      "MaxSpendUsd": 30.00,
      "SpendResetPeriod": "Daily"
    },
    {
      "Name": "Ollama-Free",
      "Priority": 3
    }
  ]
}
```

When GPT-4o hits $50, failover to Claude. When Claude hits $30, failover to free Ollama!

See [METRICS-AND-MONITORING.md](docs/METRICS-AND-MONITORING.md) for complete metrics documentation.

## Example Applications

The library includes three complete example applications demonstrating different use cases and strategies:

### Example 1: Cost-Optimized Translator

**Location**: `examples/01-SimpleTranslator/`

A translation service using **Failover strategy** to minimize costs while maintaining quality.

**Strategy**: Free models first, paid as fallback
- **Primary**: EasyNMT (free, specialized translation)
- **Secondary**: Ollama qwen2.5:1.5b (free, general purpose)
- **Fallback**: OpenAI GPT-4o-mini (paid, $2 daily budget)

**Features**:
- Transparent backend selection
- Batch translation with per-backend statistics
- Budget protection on paid backends
- HTML welcome page with API documentation

**Endpoints**:
- `POST /translate` - Single translation
- `POST /translate/batch` - Batch processing
- `GET /health` - Backend health status
- `GET /` - Interactive documentation

**Run It**:
```bash
cd examples/01-SimpleTranslator
dotnet run

# Test it
curl -X POST http://localhost:5000/translate \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello world", "targetLanguage": "es"}'
```

**Key Learning**: How to optimize costs with free local models and paid fallbacks.

---

### Example 2: Multi-Model Chat Interface

**Location**: `examples/02-ChatInterface/`

A chat application using **Specific strategy** with user-selectable models.

**Models Available**:
- Ollama Llama 3 (free local)
- OpenAI GPT-4o-mini (paid)
- Anthropic Claude 3.5 Sonnet (paid)

**Features**:
- In-memory conversation history management
- Rate limiting (60 req/min, 5 concurrent)
- Real-time cost tracking per conversation
- Model selection via UI
- Web-based chat interface with JavaScript

**Endpoints**:
- `POST /chat` - Send message
- `GET /conversations/{id}` - Get conversation
- `DELETE /conversations/{id}` - Clear conversation
- `GET /models` - List available models
- `GET /` - Chat UI

**Run It**:
```bash
cd examples/02-ChatInterface
dotnet run

# Open browser
open http://localhost:5000
```

**Key Learning**: How to manage conversations and let users choose models.

---

### Example 3: Collaborative SciFi Story Writer

**Location**: `examples/03-SciFiStoryWriter/`

A creative writing tool using **Simultaneous strategy** (NEW!) for multi-LLM collaboration.

**Strategy**: Get multiple creative variations from different LLMs
- Claude 3.5 Sonnet (literary prose)
- GPT-4o (plot structure)
- Llama 3 (creative twists)

**How It Works**:
1. **Generate Story Beats** → Get 3 variations from 3 LLMs
2. **User Selects Favorite** → Pick the best outline
3. **Write Page 1** → Get 3 creative versions
4. **User Selects Best** → Choose favorite prose
5. **Write Page 2** → Cumulative context from previous selections
6. **Repeat** → Build story page-by-page
7. **Export** → Download complete markdown story

**Features**:
- Parallel multi-LLM calls with Simultaneous strategy
- Side-by-side comparison of different models
- Cumulative context building (each page sees previous selections)
- Markdown export with metadata
- Retro terminal-style UI (green on black)
- Budget limits ($5 daily) on paid models

**Endpoints**:
- `POST /beats` - Generate story beats (3 variations)
- `POST /select/{id}/beats` - Select favorite beats
- `POST /page/{id}` - Generate next page (3 variations)
- `POST /select/{id}/page` - Add selected page
- `GET /story/{id}` - View story status
- `GET /export/{id}` - Download markdown
- `GET /stories` - List all stories

**Example Workflow**:
```bash
cd examples/03-SciFiStoryWriter
dotnet run

# 1. Generate beats
curl -X POST http://localhost:5000/beats \
  -H "Content-Type: application/json" \
  -d '{
    "genre": "Cyberpunk",
    "themes": ["AI consciousness", "corporate dystopia"],
    "setting": "Neo-Tokyo 2157",
    "tone": "Dark and gritty"
  }'

# Returns 3 beat variations from Claude, GPT-4o, and Llama 3
# Response: { "storyId": "abc-123", "variations": [...] }

# 2. Select your favorite beats
curl -X POST http://localhost:5000/select/abc-123/beats \
  -d '{"selectedText": "...", "backend": "gpt4o-plotter", "model": "gpt-4o"}'

# 3. Generate first page (gets 3 versions)
curl -X POST http://localhost:5000/page/abc-123

# 4. Select best version
curl -X POST http://localhost:5000/select/abc-123/page \
  -d '{"selectedText": "...", "backend": "claude-writer", "model": "claude-3-5-sonnet-20241022"}'

# 5. Repeat for each page

# 6. Export final story
curl http://localhost:5000/export/abc-123 > my-story.md
```

**Key Learning**:
- How to use Simultaneous strategy for creative tasks
- Comparing outputs from different LLMs
- Building cumulative context across multiple LLM calls
- Cherry-picking the best parts from different models

**Why This is Awesome**:
- See how Claude excels at prose, GPT-4o at plot structure
- Different models = different creative perspectives
- Build consensus or pick the best from each
- Educational: understand LLM strengths and weaknesses
- Cost-conscious: free Llama 3 included for comparison

---

### Running the Examples

All examples use .NET 8.0 minimal APIs and include:
- Complete standalone .csproj files
- Interactive HTML documentation pages
- Budget tracking and cost transparency
- Prometheus metrics integration

**Setup**:
```bash
# Set your API keys
export OPENAI_API_KEY=sk-...
export ANTHROPIC_API_KEY=sk-ant-...

# Optional: Start Ollama for free local models
ollama pull llama3
ollama pull qwen2.5:1.5b
ollama serve

# Run any example
cd examples/XX-ExampleName
dotnet run

# Visit http://localhost:5000 for interactive docs
```

## Best Practices

1. **Use Failover Strategy** for production with multiple backends
2. **Set Budget Limits** (NEW!) to prevent runaway costs with automatic backend disabling
3. **Enable Circuit Breakers** to prevent cascading failures
4. **Configure Health Checks** to automatically detect and skip unhealthy backends
5. **Use Secrets Management** (never commit API keys to source control)
6. **Enable Caching** for repeated queries to reduce costs
7. **Monitor Statistics** to optimize backend selection and performance
8. **Set Timeouts** appropriate for your use case
9. **Configure Rate Limits** to stay within API quotas
10. **Enable Telemetry** for production observability
11. **Use Cost Tracking** to monitor and optimize expenses
12. **Try Simultaneous Strategy** (NEW!) for creative tasks where quality > speed
13. **Use Prometheus Metrics** to monitor budget usage and performance in Grafana

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
- **[Metrics and Monitoring Guide](docs/METRICS-AND-MONITORING.md)** - Prometheus/Grafana setup and budget tracking
- **[Testing Guide](docs/TESTING-GUIDE.md)** - Unit testing with fakes and mocks

### Configuration Examples
- **[Complete Configuration Example](examples/appsettings.example.json)** - Fully commented configuration template

### Example Applications
- **[01-SimpleTranslator](examples/01-SimpleTranslator/)** - Cost-optimized translation with failover
- **[02-ChatInterface](examples/02-ChatInterface/)** - Multi-model chat with conversation history
- **[03-SciFiStoryWriter](examples/03-SciFiStoryWriter/)** - Collaborative writing with Simultaneous strategy

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
