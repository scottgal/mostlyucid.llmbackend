# ResXTranslator Integration Guide

This guide shows how to update ResXTranslator to use the new Mostlyucid.LlmBackend library.

## Table of Contents
- [Migration Overview](#migration-overview)
- [Installation](#installation)
- [Configuration Changes](#configuration-changes)
- [Code Migration](#code-migration)
- [Benefits of Migration](#benefits-of-migration)

## Migration Overview

The ResXTranslator project currently has its own embedded LLM backend code. By migrating to the standalone `Mostlyucid.LlmBackend` NuGet package, you get:

- ✅ **New LLM Providers**: Claude, Gemini, Cohere
- ✅ **Enterprise Features**: Circuit breakers, rate limiting, caching
- ✅ **Better Reliability**: Automatic failover and retry logic
- ✅ **Cost Tracking**: Monitor translation costs
- ✅ **Future Updates**: Get new providers and features automatically

## Installation

### Update Project Reference

In `Mostlylucid.ResxTranslator.Core.csproj`, replace the project reference with the NuGet package:

**Before:**
```xml
<ItemGroup>
  <ProjectReference Include="..\Mostlyucid.LlmBackend\Mostlyucid.LlmBackend.csproj" />
</ItemGroup>
```

**After:**
```xml
<ItemGroup>
  <PackageReference Include="Mostlyucid.LlmBackend" Version="2.0.0" />
</ItemGroup>
```

Or use project reference during development:
```xml
<ItemGroup>
  <ProjectReference Include="..\..\mostlyucid.llmbackend\Mostlyucid.LlmBackend.csproj" />
</ItemGroup>
```

## Configuration Changes

### Old Configuration (appsettings.json)

The old configuration was simpler but less powerful:

```json
{
  "LlmSettings": {
    "SelectionStrategy": "Failover",
    "Backends": [
      {
        "Name": "OpenAI",
        "Type": "OpenAI",
        "BaseUrl": "https://api.openai.com",
        "ApiKey": "sk-...",
        "ModelName": "gpt-4o"
      }
    ]
  }
}
```

### New Configuration (Enhanced)

The new configuration adds enterprise features:

```json
{
  "LlmSettings": {
    "SelectionStrategy": "Failover",
    "TimeoutSeconds": 120,
    "MaxRetries": 3,
    "UseExponentialBackoff": true,
    "DefaultTemperature": 0.3,
    "DefaultMaxTokens": 2000,

    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 3,
      "DurationOfBreakSeconds": 30
    },

    "RateLimit": {
      "Enabled": true,
      "MaxRequests": 50,
      "WindowSeconds": 60,
      "MaxConcurrentRequests": 5
    },

    "Caching": {
      "Enabled": true,
      "Provider": "Memory",
      "ExpirationMinutes": 1440,
      "KeyPrefix": "resx:"
    },

    "Telemetry": {
      "EnableMetrics": true,
      "EnableCostTracking": true,
      "ServiceName": "ResXTranslator"
    },

    "Backends": [
      {
        "Name": "Primary-GPT4",
        "Type": "OpenAI",
        "BaseUrl": "https://api.openai.com",
        "ApiKey": "${RESX_OPENAI_APIKEY}",
        "ModelName": "gpt-4o",
        "Temperature": 0.3,
        "Priority": 1,
        "Enabled": true,
        "CostPerMillionInputTokens": 5.0,
        "CostPerMillionOutputTokens": 15.0
      },
      {
        "Name": "Fallback-Claude",
        "Type": "Anthropic",
        "BaseUrl": "https://api.anthropic.com",
        "ApiKey": "${RESX_ANTHROPIC_APIKEY}",
        "ModelName": "claude-3-5-sonnet-20241022",
        "Temperature": 0.3,
        "Priority": 2,
        "Enabled": true,
        "CostPerMillionInputTokens": 3.0,
        "CostPerMillionOutputTokens": 15.0
      },
      {
        "Name": "Translation-EasyNMT",
        "Type": "EasyNMT",
        "BaseUrl": "http://localhost:24080",
        "ModelName": "opus-mt",
        "Priority": 3,
        "Enabled": false
      }
    ]
  }
}
```

### Recommended Configuration for Translation

For translation workloads, these settings work well:

```json
{
  "LlmSettings": {
    "SelectionStrategy": "Failover",
    "DefaultTemperature": 0.3,
    "DefaultMaxTokens": 2000,

    "Caching": {
      "Enabled": true,
      "Provider": "Memory",
      "ExpirationMinutes": 1440,
      "MaxCacheSizeMb": 500,
      "KeyPrefix": "resx:"
    },

    "Telemetry": {
      "EnableCostTracking": true
    },

    "Backends": [
      {
        "Name": "GPT-4-Turbo",
        "Type": "OpenAI",
        "ModelName": "gpt-4-turbo-preview",
        "Temperature": 0.3,
        "Priority": 1
      },
      {
        "Name": "Claude-Sonnet",
        "Type": "Anthropic",
        "ModelName": "claude-3-5-sonnet-20241022",
        "Temperature": 0.3,
        "Priority": 2
      }
    ]
  }
}
```

## Code Migration

### No Changes Required!

The good news is that the new library maintains **100% API compatibility** with the old embedded version. Your existing code should work without any changes:

```csharp
// This code works with both old and new library
public class TranslationService
{
    private readonly ILlmService _llmService;
    private readonly IPromptBuilder _promptBuilder;

    public TranslationService(
        ILlmService llmService,
        IPromptBuilder promptBuilder)
    {
        _llmService = llmService;
        _promptBuilder = promptBuilder;
    }

    public async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string? domain = null)
    {
        var context = new PromptContext
        {
            SystemMessage = "You are a professional translator",
            ContextVariables = new Dictionary<string, string>
            {
                ["sourceLanguage"] = sourceLanguage,
                ["targetLanguage"] = targetLanguage,
                ["domain"] = domain ?? "general",
                ["text"] = text
            }
        };

        var chatRequest = _promptBuilder.BuildChatRequest(
            $"Translate the following text from {sourceLanguage} to {targetLanguage}",
            context
        );

        var response = await _llmService.ChatAsync(chatRequest);
        return response.Content;
    }
}
```

### Optional: Use New Features

You can optionally enhance your translation service with new features:

```csharp
public class EnhancedTranslationService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<EnhancedTranslationService> _logger;

    public async Task<TranslationResult> TranslateWithMetricsAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = "You are a professional translator" },
                new() { Role = "user", Content = BuildTranslationPrompt(text, sourceLanguage, targetLanguage) }
            },
            Temperature = 0.3,
            MaxTokens = 2000
        };

        var response = await _llmService.ChatAsync(request);

        // Return enhanced result with metrics
        return new TranslationResult
        {
            TranslatedText = response.Content,
            BackendUsed = response.BackendUsed,
            ModelUsed = response.ModelUsed,
            DurationMs = response.DurationMs,
            TokensUsed = response.TotalTokens,
            Success = response.Success
        };
    }

    private string BuildTranslationPrompt(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return $"""
            Translate the following text from {sourceLanguage} to {targetLanguage}.
            Preserve all formatting placeholders (like {{0}}, {{name}}, etc.).
            Maintain the same tone and style.

            Text to translate:
            {text}

            Provide only the translation, without explanations.
            """;
    }
}

public record TranslationResult(
    string TranslatedText,
    string BackendUsed,
    string ModelUsed,
    long DurationMs,
    int TokensUsed,
    bool Success
);
```

## Benefits of Migration

### 1. Multiple Provider Support

Now you can use different providers for different scenarios:

```csharp
// Use Claude for long documents (200K context window)
var longDocRequest = new LlmRequest
{
    Prompt = veryLongDocument,
    BackendName = "Claude-Sonnet"
};

// Use GPT-4 for complex translations
var complexRequest = new LlmRequest
{
    Prompt = technicalText,
    BackendName = "GPT-4-Turbo"
};

// Use EasyNMT for simple translations (free!)
var simpleRequest = new LlmRequest
{
    Prompt = simpleText,
    BackendName = "Translation-EasyNMT"
};
```

### 2. Automatic Caching

Translations are now cached automatically, saving costs:

```csharp
// First call - hits the API
var translation1 = await TranslateAsync("Hello", "en", "fr");

// Second call - served from cache (free!)
var translation2 = await TranslateAsync("Hello", "en", "fr");
```

### 3. Cost Tracking

Monitor your translation costs:

```csharp
public class TranslationCostTracker
{
    private readonly ILlmService _llmService;

    public decimal GetTotalTranslationCosts()
    {
        var stats = _llmService.GetStatistics();

        decimal totalCost = 0;
        foreach (var stat in stats)
        {
            // Get backend config to get pricing
            // Calculate: (tokens / 1M) * cost_per_million
            // This requires storing per-request token counts
        }

        return totalCost;
    }
}
```

### 4. Better Error Handling

Automatic failover means translations don't fail if one provider is down:

```json
{
  "Backends": [
    { "Name": "Primary", "Priority": 1 },
    { "Name": "Backup", "Priority": 2 },
    { "Name": "Emergency", "Priority": 3 }
  ]
}
```

If Primary fails → tries Backup → tries Emergency

### 5. Performance Monitoring

Track which backends perform best:

```csharp
var stats = _llmService.GetStatistics();
foreach (var stat in stats)
{
    Console.WriteLine($"{stat.Name}:");
    Console.WriteLine($"  Avg Response Time: {stat.AverageResponseTimeMs}ms");
    Console.WriteLine($"  Success Rate: {stat.SuccessfulRequests}/{stat.TotalRequests}");
}
```

## Migration Steps

### Step 1: Update Project File

Remove old embedded LlmBackend folder and add package reference:

```bash
# Remove old code
rm -rf src/Mostlyucid.LlmBackend/

# Add package reference
dotnet add src/Mostlylucid.ResxTranslator.Core package Mostlyucid.LlmBackend
```

### Step 2: Update Configuration

Enhance your appsettings.json with new features (optional but recommended):

```json
{
  "LlmSettings": {
    "Caching": { "Enabled": true },
    "CircuitBreaker": { "Enabled": true },
    "Telemetry": { "EnableCostTracking": true }
  }
}
```

### Step 3: Test

Your existing code should work without changes:

```bash
dotnet build
dotnet test
```

### Step 4: Deploy

Deploy with confidence - the API is compatible!

## Recommended Settings for Production

```json
{
  "LlmSettings": {
    "SelectionStrategy": "Failover",
    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 3,
      "DurationOfBreakSeconds": 30
    },
    "RateLimit": {
      "Enabled": true,
      "MaxRequests": 50,
      "WindowSeconds": 60
    },
    "Caching": {
      "Enabled": true,
      "Provider": "Memory",
      "ExpirationMinutes": 1440
    },
    "Secrets": {
      "Provider": "EnvironmentVariables",
      "EnvironmentVariablePrefix": "RESX_"
    },
    "Telemetry": {
      "EnableMetrics": true,
      "EnableCostTracking": true
    },
    "Backends": [
      {
        "Name": "Primary",
        "Type": "OpenAI",
        "ModelName": "gpt-4o",
        "Temperature": 0.3,
        "Priority": 1,
        "Enabled": true
      },
      {
        "Name": "Fallback",
        "Type": "Anthropic",
        "ModelName": "claude-3-5-sonnet-20241022",
        "Temperature": 0.3,
        "Priority": 2,
        "Enabled": true
      }
    ]
  }
}
```

## Troubleshooting

### Issue: Build errors after migration
**Solution**: Clean and rebuild:
```bash
dotnet clean
dotnet build
```

### Issue: Different translation results
**Solution**: Ensure you're using the same temperature (0.3 recommended for translation)

### Issue: Cache not working
**Solution**: Check that caching is enabled in configuration

## Next Steps

1. ✅ Remove embedded LlmBackend code
2. ✅ Add package reference
3. ✅ Update configuration (optional)
4. ✅ Test thoroughly
5. ✅ Deploy to production
6. ✅ Monitor costs and performance
7. ✅ Enjoy new providers and features!

## Summary

The migration is **painless** because:
- ✅ API is 100% compatible
- ✅ No code changes required
- ✅ Configuration is backwards compatible
- ✅ You get new features for free
- ✅ Future updates via NuGet

**Recommendation**: Migrate to the package to benefit from ongoing improvements and new providers!
