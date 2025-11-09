---
date: 2025-01-09T14:30:00
categories: [.NET, LLM, AI, Machine Learning, ASP.NET Core]
title: "Mostlylucid.LlmBackend: A Universal LLM Backend for .NET with Budget Controls and Multi-Model Responses"
---

# Introduction

After three years of building LLM-powered applications, I've finally built the abstraction layer I wish existed from day one: **mostlylucid.llmbackend**. It's a production-ready .NET library that treats OpenAI, Claude, Gemini, Ollama, and a dozen other LLM providers as interchangeable backends with automatic failover, real-time budget enforcement, and - my favorite feature - the ability to call multiple LLMs simultaneously and compare their responses.

This isn't another thin wrapper around OpenAI's SDK. This is enterprise infrastructure with features you actually need: circuit breakers that prevent cascading failures, Prometheus metrics for Grafana dashboards, automatic budget limits that prevent surprise bills, and a plugin system for adding custom providers without touching core code.

# The Problem This Solves

Let me paint a picture you've probably lived:

You start with OpenAI because it's easy. Then you discover Claude is better at certain tasks. Then you want Llama 3 running locally for privacy-sensitive operations. Suddenly you're maintaining three different API integrations with completely different request/response formats, retry logic, error handling, and billing models.

Then your OpenAI bill hits $500 because someone left a test chatbot running over the weekend. You scramble to add budget controls - but now you have three different billing implementations to maintain.

Then OpenAI goes down. Your entire application is offline because you're tightly coupled to one vendor.

I got tired of this cycle, so I built mostlylucid.llmbackend to solve all of it at once.

# Supported Providers

The library currently supports:

- **OpenAI** - GPT-3.5, GPT-4, GPT-4 Turbo, GPT-4o, o1-preview, o1-mini
- **Anthropic Claude** - Claude 3 (Opus, Sonnet, Haiku), Claude 3.5 Sonnet
- **Google Gemini** - Gemini Pro, Gemini Ultra (both AI Studio and Vertex AI)
- **Azure OpenAI** - All Azure OpenAI deployments
- **Ollama** - Llama 3, Mistral, qwen2.5, and 100+ other local models
- **LM Studio** - Local model serving
- **EasyNMT & mostlylucid-nmt** - Specialized translation models (more on this later)
- **Cohere** - Command, Command R, Command R+
- **Generic OpenAI-compatible APIs** - Mistral, Together AI, Perplexity, and more

Plus a plugin system for adding custom providers.

# Backend Selection Strategies

The library ships with six selection strategies:

## 1. Failover (Priority-Based)

Try backends in priority order until one succeeds. Perfect for "use GPT-4, fall back to Claude if it's down, fall back to local Ollama if both fail."

```json
{
  "SelectionStrategy": "Failover",
  "Backends": [
    { "Name": "GPT-4o", "Priority": 1, "Enabled": true },
    { "Name": "Claude-3.5", "Priority": 2, "Enabled": true },
    { "Name": "Local-Llama", "Priority": 3, "Enabled": true }
  ]
}
```

## 2. RoundRobin (Load Balancing)

Distribute requests evenly across all backends. Great for cost optimization and load distribution.

## 3. LowestLatency (Performance-Based)

Route to the backend with the best average response time. The library tracks latency and automatically favors faster backends.

## 4. Specific (User Choice)

Let users or code explicitly choose which backend to use:

```csharp
var response = await llm.CompleteAsync(new LlmRequest
{
    Prompt = "Translate this to French",
    PreferredBackend = "Claude-3.5"
});
```

## 5. Random

Sometimes you just want chaos. Or A/B testing without bias.

## 6. Simultaneous (NEW!)

This is the exciting one. Call multiple LLMs **in parallel** and get all responses back:

```csharp
var response = await llm.CompleteAsync(new LlmRequest
{
    Prompt = "Write a creative opening for a cyberpunk story"
});

// Primary response (first successful)
Console.WriteLine($"Primary ({response.Backend}): {response.Content}");

// Get all alternative responses
foreach (var alt in response.AlternativeResponses ?? new())
{
    Console.WriteLine($"\n{alt.Backend}: {alt.Content}");
}
```

You get three completely different creative takes on the same prompt. Perfect for:
- Creative writing (compare Claude's prose vs GPT-4's structure)
- A/B testing model outputs
- Educational comparisons of LLM capabilities
- Quality assurance (pick the best response)
- Building consensus from multiple models

# Automatic Budget Limits - No More Surprise Bills

Remember that $500 OpenAI bill? This is how you prevent it:

```json
{
  "Backends": [{
    "Name": "GPT-4o",
    "Type": "OpenAI",
    "ModelName": "gpt-4o",
    "CostPerMillionInputTokens": 2.50,
    "CostPerMillionOutputTokens": 10.00,
    "MaxSpendUsd": 10.00,
    "SpendResetPeriod": "Daily",
    "LogBudgetExceeded": true
  }]
}
```

**How It Works:**

1. Every request tracks actual token usage
2. Costs are calculated in real-time based on configured pricing
3. When `MaxSpendUsd` is hit, the backend **automatically gets disabled**
4. At the configured reset time (midnight UTC for daily), it re-enables
5. Failover strategy automatically uses your next available backend

**Reset Periods:**
- `Daily` - Reset at midnight UTC
- `Weekly` - Reset on configured day (default: Monday)
- `Monthly` - Reset on configured day of month (default: 1st)
- `Never` - Manual reset only

This isn't "please stay under budget." It's **enforced**. The backend becomes unavailable when the limit is hit. Your failover strategy will use the next backend automatically.

## Prometheus Metrics for Budget Tracking

The budget tracking integrates with Prometheus:

```
llm_backend_budget_usd{backend="GPT-4o",limit_type="current"} 7.32
llm_backend_budget_usd{backend="GPT-4o",limit_type="max"} 10.00
```

Set up Grafana alerts:
```yaml
- alert: LLMBudgetNearLimit
  expr: |
    (llm_backend_budget_usd{limit_type="current"}
    /
    llm_backend_budget_usd{limit_type="max"}) > 0.9
  annotations:
    summary: "Backend {{ $labels.backend }} has used 90% of budget"
```

# Real-World Example: Cost-Optimized Translation with mostlylucid-nmt

I integrated my [mostlylucid-nmt](https://github.com/scottgal/mostlylucid-nmt) neural machine translation service with this library to create a cost-optimized translation pipeline:

```csharp
settings.SelectionStrategy = BackendSelectionStrategy.Failover;
settings.Backends = new List<LlmBackendConfig>
{
    // PRIMARY: mostlylucid-nmt - Free, specialized, fast (~0.3-1.0s/sentence)
    new()
    {
        Name = "EasyNMT-Local",
        Type = LlmBackendType.EasyNMT,
        BaseUrl = "http://localhost:24080",
        ModelName = "opus-mt",  // Uses Opus-MT, mBART50, or M2M100
        Priority = 1,
        Enabled = true
    },

    // SECONDARY: Ollama - Free, general purpose, local
    new()
    {
        Name = "Ollama-Qwen-1.5B",
        Type = LlmBackendType.Ollama,
        BaseUrl = "http://localhost:11434",
        ModelName = "qwen2.5:1.5b",
        Temperature = 0.3,
        Priority = 2,
        Enabled = true
    },

    // FALLBACK: OpenAI - Paid with budget protection
    new()
    {
        Name = "OpenAI-Fallback",
        Type = LlmBackendType.OpenAI,
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        ModelName = "gpt-4o-mini",
        Temperature = 0.3,
        Priority = 3,
        CostPerMillionInputTokens = 0.15m,
        CostPerMillionOutputTokens = 0.60m,
        MaxSpendUsd = 2.00m,
        SpendResetPeriod = SpendResetPeriod.Daily,
        Enabled = true
    }
};
```

**The Translation Pathway:**

1. **Try mostlylucid-nmt first** - Specialized neural MT models (Opus-MT with 1200+ language pairs, mBART50, or M2M100). Runs on CPU in ~0.3-1.0 seconds per sentence. Cost: $0.
2. **Fall back to Ollama qwen2.5** - General-purpose LLM running locally. Slower but still free. Good for languages mostlylucid-nmt doesn't support well.
3. **Last resort: OpenAI GPT-4o-mini** - Paid API with $2/day budget limit. Only used when both local options fail or for edge cases.

**Result:** 95% of translations are completely free. The 5% that need OpenAI are budget-protected and automatically fall out of use when the limit is hit.

This is exactly the kind of tiered failover that makes sense in production - free specialized models first, free general models second, paid APIs with budget controls as a safety net.

The mostlylucid-nmt integration demonstrates why backend abstraction matters. I can swap between neural MT (fast, specialized, free) and LLMs (slower, flexible, sometimes costly) without changing application code. The library handles failover, retry logic, and budget tracking automatically.

# Example: Collaborative Story Writer with Simultaneous Strategy

This example showcases the Simultaneous strategy in action. It's a page-by-page story builder where you get three versions of each element from different LLMs:

**The Workflow:**

1. User provides genre, themes, setting, and tone
2. Library calls Claude, GPT-4o, and Llama 3 **in parallel**
3. User sees three different story outlines
4. User picks their favorite
5. Repeat for each page, building cumulative context
6. Export final story as markdown

**Each Model's Strengths:**
- **Claude 3.5 Sonnet** - Beautiful, literary prose with nuanced character development
- **GPT-4o** - Excellent plot structure, pacing, and narrative arc
- **Llama 3** - Unexpected creative twists and unconventional approaches

The user cherry-picks the best ideas from each model. Maybe Claude writes beautiful prose, but GPT-4o had a better plot twist, and Llama 3 suggested an interesting character detail. You combine all three.

**Code Example:**

```csharp
// Configure Simultaneous strategy
settings.SelectionStrategy = BackendSelectionStrategy.Simultaneous;
settings.Backends = new List<LlmBackendConfig>
{
    new() {
        Name = "claude-writer",
        Type = LlmBackendType.Anthropic,
        ModelName = "claude-3-5-sonnet-20241022",
        Temperature = 0.9,  // High creativity
        MaxSpendUsd = 5.00m,
        SpendResetPeriod = SpendResetPeriod.Daily
    },
    new() {
        Name = "gpt4o-plotter",
        Type = LlmBackendType.OpenAI,
        ModelName = "gpt-4o",
        Temperature = 0.9,
        MaxSpendUsd = 5.00m,
        SpendResetPeriod = SpendResetPeriod.Daily
    },
    new() {
        Name = "llama3-creative",
        Type = LlmBackendType.Ollama,
        ModelName = "llama3",
        Temperature = 0.9
    }
};

// Generate story beats - get 3 variations
var response = await llm.CompleteAsync(new LlmRequest
{
    Prompt = $"""
        Generate 3-5 story beats for a {genre} story about {themes}.
        Setting: {setting}
        Tone: {tone}
        """,
    Temperature = 0.9
});

// Collect all variations
var allBeats = new List<BeatVariation>
{
    new() {
        Backend = response.Backend,
        Beats = response.Content,
        TokensUsed = response.TotalTokens ?? 0
    }
};

// Add alternative responses from other backends
foreach (var alt in response.AlternativeResponses ?? new())
{
    allBeats.Add(new BeatVariation {
        Backend = alt.Backend,
        Beats = alt.Content,
        TokensUsed = alt.TotalTokens ?? 0
    });
}

// User selects their favorite, then repeat for each page
```

The full example is in `examples/03-SciFiStoryWriter/` with a retro terminal UI (green on black, naturally).

# Enterprise Features

## Circuit Breakers

Automatic circuit breaking when a backend starts failing repeatedly:

```json
{
  "CircuitBreaker": {
    "Enabled": true,
    "FailureThreshold": 5,
    "DurationOfBreakSeconds": 30,
    "SamplingDurationSeconds": 60
  }
}
```

After 5 failures in 60 seconds, the circuit opens for 30 seconds. Prevents cascading failures.

## Rate Limiting

Stay within API quotas:

```json
{
  "RateLimit": {
    "Enabled": true,
    "MaxRequests": 100,
    "WindowSeconds": 60,
    "MaxConcurrentRequests": 10,
    "QueueLimit": 100
  }
}
```

## Health Checks

Automatic backend health monitoring. Unhealthy backends are automatically skipped:

```csharp
var backends = await llmService.TestBackendsAsync();
foreach (var (name, health) in backends)
{
    Console.WriteLine($"{name}: {(health.IsHealthy ? "✓" : "✗")} " +
                     $"({health.AverageLatencyMs}ms avg)");
}
```

## Comprehensive Metrics

Out-of-the-box Prometheus metrics:

```
llm_requests_total{backend="gpt-4o",status="success"} 1247
llm_request_duration_seconds_bucket{backend="gpt-4o",le="1.0"} 892
llm_tokens_total{backend="gpt-4o",token_type="completion"} 124523
llm_estimated_cost_usd{backend="gpt-4o"} 12.34
llm_backend_health{backend="gpt-4o"} 1
llm_active_requests{backend="gpt-4o"} 3
```

Wire this into Grafana and you have real-time visibility into:
- Request rates and success/failure ratios
- P50/P95/P99 latency
- Token usage trends
- Cost per hour/day/month
- Backend health status
- Current spend vs budget limits

## Caching

Redis, Memory, or SQL Server caching to stop paying for the same completion twice:

```json
{
  "Caching": {
    "Enabled": true,
    "Provider": "Redis",
    "ConnectionString": "localhost:6379",
    "ExpirationMinutes": 60,
    "KeyPrefix": "llm:"
  }
}
```

## Secrets Management

Never commit API keys:

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "KeyVaultUrl": "https://your-vault.vault.azure.net/",
    "UseManagedIdentity": true
  }
}
```

Supports Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, Google Secret Manager, and environment variables.

# Quick Start - Zero Configuration

Want to just get started? Here's the absolute minimum:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLlmBackend(settings =>
{
    settings.Backends = new List<LlmBackendConfig>
    {
        new()
        {
            Name = "OpenAI",
            Type = LlmBackendType.OpenAI,
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            ModelName = "gpt-4o"
        }
    };
});

var app = builder.Build();

app.MapGet("/ask", async (ILlmService llm, string question) =>
{
    var response = await llm.CompleteAsync(new LlmRequest { Prompt = question });
    return response.Content;
});

app.Run();
```

That's it. Everything else (timeouts, retries, circuit breaking, metrics, logging) has sensible defaults.

Or use a local model for $0:

```json
{
  "LlmSettings": {
    "Backends": [{
      "Name": "Ollama",
      "Type": "Ollama",
      "BaseUrl": "http://localhost:11434",
      "ModelName": "llama3"
    }]
  }
}
```

# The Plugin System

Don't see your favorite provider? Write a plugin:

```csharp
public class MyLlmPlugin : ILlmBackendPlugin
{
    public string PluginId => "com.mycompany.llmprovider";
    public IEnumerable<string> SupportedBackendTypes => new[] { "MyLLM" };

    public ILlmBackend CreateBackend(LlmBackendConfig config, IServiceProvider services)
    {
        return new MyLlmBackend(config, services);
    }
}
```

Drop the DLL in `plugins/`, add configuration, and it automatically gets:
- Failover support
- Retry logic
- Circuit breaking
- Metrics
- Health checks
- Budget tracking
- Caching

You just implement "call this API and parse the response."

# Testing Support

No more mocking HTTP clients or paying for test API calls:

```csharp
var fakeLlm = new FakeLlmBackend("test-backend");
fakeLlm.AddResponse("what is 2+2", "4");
fakeLlm.AddResponse("translate 'hello'", "bonjour");

var response = await fakeLlm.CompleteAsync(new LlmRequest { Prompt = "what is 2+2" });
Assert.Equal("4", response.Content);
```

Instant, deterministic, free. See `docs/TESTING-GUIDE.md` for full documentation.

# Performance Notes

**Simultaneous Strategy:**
Obviously slower than single-backend calls (you're waiting for the slowest LLM). But for creative tasks where quality matters more than speed, it's incredible. You're essentially running a writers' room of AIs.

**Budget Tracking Overhead:**
Negligible - a couple of `Interlocked` operations and a timestamp check per request. I've run this in production handling thousands of requests per minute with no measurable impact.

**Failover Performance:**
The library tracks average response times and automatically favors faster backends when using `LowestLatency` strategy. Failed requests skip to the next backend immediately - no wasted time.

# Example Applications

The library ships with three complete example applications:

## 1. Cost-Optimized Translator
Demonstrates failover with mostlylucid-nmt integration. Free specialized models → free general models → paid with budget.

## 2. Multi-Model Chat Interface
User-selectable backends with conversation history. Shows how to manage state across different model types.

## 3. Collaborative Story Writer
Simultaneous strategy in action. Page-by-page story building with multiple LLMs providing creative variations.

All examples include full `.csproj` files, HTML interfaces, and detailed documentation.

# Why I Built This

I was tired of:
- Rewriting retry logic for each provider
- Getting surprised by API bills
- Vendor lock-in making it hard to switch models
- Copy-pasting backend code between projects
- Not being able to A/B test different models easily
- Maintaining separate budget tracking for each provider
- No unified metrics across different LLM APIs

This library solves all of that. It's the abstraction layer I wish existed when I started building LLM-powered applications.

The budget controls alone have saved me hundreds of dollars. The Simultaneous strategy has made creative writing projects genuinely fun. And the mostlylucid-nmt integration shows how powerful it is to treat neural MT and LLMs as interchangeable backends.

# Get Started

```bash
dotnet add package Mostlyucid.LlmBackend
```

**GitHub**: [scottgal/mostlyucid.llmbackend](https://github.com/scottgal/mostlyucid.llmbackend)

**Documentation:**
- Complete configuration guide in README
- Three example applications with full source
- Prometheus/Grafana setup guide
- Testing guide with fakes
- Plugin development guide

**License**: MIT - use it for whatever

**Status**: Production-ready, actively maintained, used in multiple projects including this blog's translation pipeline

Now go build something cool. And set those budget limits. Trust me on this one.

# Conclusion

If you're building anything serious with LLMs in .NET, this library will save you weeks of infrastructure work. The abstraction layer makes it trivial to switch between providers, test different models, or add fallbacks. The budget controls prevent surprise bills. The Prometheus metrics give you real-time visibility. And the Simultaneous strategy opens up entirely new ways to use LLMs creatively.

I've been running this in production for several months now across multiple projects. It's stable, performant, and has genuinely made LLM integration simpler.

Questions? Issues? The GitHub repo is open for PRs and issues. I'm actively maintaining this and happy to help with integration questions.
