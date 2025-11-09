# Plugin Development Guide

This guide explains how to create custom LLM backend plugins for mostlylucid.llmbackend.

## Table of Contents
- [Overview](#overview)
- [Plugin Architecture](#plugin-architecture)
- [Creating a Plugin](#creating-a-plugin)
- [Example: Mistral AI Plugin](#example-mistral-ai-plugin)
- [Deploying Plugins](#deploying-plugins)
- [Testing Plugins](#testing-plugins)
- [Best Practices](#best-practices)

## Overview

The mostlylucid.llmbackend library supports a plugin architecture that allows you to add custom LLM providers without modifying the core library. Plugins are discovered and loaded dynamically from DLL files.

### When to Create a Plugin

Create a plugin when you want to:
- Add support for a new LLM provider not included in the core library
- Implement a custom API wrapper
- Add proprietary or internal LLM backends
- Experiment with new providers without waiting for official support

## Plugin Architecture

### Key Interfaces

**ILlmBackendPlugin** - Main plugin interface that provides metadata and backend creation
```csharp
public interface ILlmBackendPlugin
{
    string PluginId { get; }
    string PluginName { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }
    IEnumerable<string> SupportedBackendTypes { get; }

    ILlmBackend CreateBackend(
        string backendType,
        LlmBackendConfig config,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory);

    bool Validate();
}
```

**ILlmBackend** - Interface your backend implementation must inherit from
```csharp
public interface ILlmBackend
{
    string Name { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
    Task<LlmResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
    Task<BackendHealth> GetHealthAsync();
}
```

## Creating a Plugin

### Step 1: Create a New Class Library

```bash
dotnet new classlib -n MyCompany.LlmBackend.MistralPlugin
cd MyCompany.LlmBackend.MistralPlugin
```

### Step 2: Add Package Reference

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="mostlylucid.llmbackend" Version="2.0.0" />
  </ItemGroup>
</Project>
```

### Step 3: Implement Your Backend

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;

namespace MyCompany.LlmBackend.MistralPlugin;

public class MistralLlmBackend : BaseLlmBackend
{
    private const string DefaultModel = "mistral-large-latest";

    public MistralLlmBackend(
        LlmBackendConfig config,
        ILogger<MistralLlmBackend> logger,
        HttpClient httpClient)
        : base(config, logger, httpClient)
    {
        ConfigureHttpClient();
    }

    protected override void ConfigureHttpClient()
    {
        base.ConfigureHttpClient();

        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mistral backend {BackendName} is not available", Name);
            return false;
        }
    }

    public override async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var chatRequest = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = request.Prompt }
            },
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        return await ChatAsync(chatRequest, cancellationToken);
    }

    public override async Task<LlmResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var mistralRequest = new
            {
                model = _config.ModelName ?? DefaultModel,
                messages = request.Messages.Select(m => new
                {
                    role = m.Role.ToLowerInvariant(),
                    content = m.Content
                }),
                temperature = request.Temperature ?? _config.Temperature ?? 0.7,
                max_tokens = request.MaxTokens ?? _config.MaxOutputTokens ?? 2000
            };

            var json = JsonSerializer.Serialize(mistralRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Mistral request failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                RecordFailure(errorContent);
                return CreateErrorResponse(response.StatusCode.ToString(), errorContent);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var mistralResponse = JsonSerializer.Deserialize<MistralResponse>(responseBody);

            if (mistralResponse == null)
            {
                RecordFailure("Failed to deserialize response");
                return CreateErrorResponse("DeserializationError", "Failed to parse Mistral response");
            }

            RecordSuccess(stopwatch.ElapsedMilliseconds);

            return new LlmResponse
            {
                Content = mistralResponse.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty,
                BackendUsed = Name,
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ModelUsed = mistralResponse.Model ?? DefaultModel,
                PromptTokens = mistralResponse.Usage?.PromptTokens ?? 0,
                CompletionTokens = mistralResponse.Usage?.CompletionTokens ?? 0,
                TotalTokens = mistralResponse.Usage?.TotalTokens ?? 0,
                FinishReason = mistralResponse.Choices?.FirstOrDefault()?.FinishReason
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error calling Mistral backend {BackendName}", Name);
            RecordFailure(ex.Message);
            return CreateErrorResponse("Exception", ex.Message);
        }
    }

    private class MistralResponse
    {
        public string? Id { get; set; }
        public string? Object { get; set; }
        public long Created { get; set; }
        public string? Model { get; set; }
        public List<Choice>? Choices { get; set; }
        public Usage? Usage { get; set; }
    }

    private class Choice
    {
        public int Index { get; set; }
        public Message? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class Message
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    private class Usage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
```

### Step 4: Implement the Plugin Interface

```csharp
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;

namespace MyCompany.LlmBackend.MistralPlugin;

public class MistralPlugin : ILlmBackendPlugin
{
    public string PluginId => "com.mycompany.llmbackend.mistral";

    public string PluginName => "Mistral AI Plugin";

    public string Version => "1.0.0";

    public string Author => "Your Company";

    public string Description => "Adds support for Mistral AI large language models";

    public IEnumerable<string> SupportedBackendTypes => new[] { "Mistral", "MistralAI" };

    public ILlmBackend CreateBackend(
        string backendType,
        LlmBackendConfig config,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        if (!SupportedBackendTypes.Contains(backendType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Backend type {backendType} is not supported by this plugin");
        }

        var logger = loggerFactory.CreateLogger<MistralLlmBackend>();
        var httpClient = httpClientFactory.CreateClient();

        return new MistralLlmBackend(config, logger, httpClient);
    }

    public bool Validate()
    {
        // Perform any validation checks here
        // For example, check if required dependencies are available
        return true;
    }
}
```

### Step 5: Build the Plugin

```bash
dotnet build -c Release
```

The output DLL will be in `bin/Release/net8.0/MyCompany.LlmBackend.MistralPlugin.dll`

## Example: Mistral AI Plugin

See the complete example above. Key points:

1. **Inherit from BaseLlmBackend** - Provides common functionality like metrics tracking
2. **Implement ILlmBackendPlugin** - Provides plugin metadata and factory method
3. **Handle API-specific details** - Authentication, request/response formats
4. **Use proper logging** - Log errors and warnings appropriately
5. **Record metrics** - Call `RecordSuccess()` and `RecordFailure()`

## Deploying Plugins

### Method 1: Drop Into Plugins Directory

1. Create a `plugins` directory in your application root
2. Copy the plugin DLL and any dependencies
3. Configure in appsettings.json:

```json
{
  "LlmSettings": {
    "Plugins": {
      "Enabled": true,
      "PluginDirectory": "plugins",
      "LoadOnStartup": true
    }
  }
}
```

### Method 2: Specify Plugin Paths

```json
{
  "LlmSettings": {
    "Plugins": {
      "Enabled": true,
      "LoadOnStartup": true,
      "SpecificPlugins": [
        "plugins/MyCompany.LlmBackend.MistralPlugin.dll",
        "C:/CustomPlugins/AnotherPlugin.dll"
      ]
    }
  }
}
```

### Method 3: Load Programmatically

```csharp
var pluginLoader = serviceProvider.GetRequiredService<LlmPluginLoader>();
pluginLoader.LoadPluginFromAssembly("path/to/plugin.dll");
```

## Configuration

### Using Your Plugin Backend

```json
{
  "LlmSettings": {
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
}
```

**Important**: Set `CustomBackendType` to match one of the types in `SupportedBackendTypes`

## Testing Plugins

### Unit Tests

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;

public class MistralPluginTests
{
    [Fact]
    public void Plugin_HasCorrectMetadata()
    {
        var plugin = new MistralPlugin();

        Assert.Equal("com.mycompany.llmbackend.mistral", plugin.PluginId);
        Assert.Equal("Mistral AI Plugin", plugin.PluginName);
        Assert.Contains("Mistral", plugin.SupportedBackendTypes);
    }

    [Fact]
    public void Plugin_ValidatesSuccessfully()
    {
        var plugin = new MistralPlugin();
        Assert.True(plugin.Validate());
    }

    [Fact]
    public void Plugin_CreatesBackend()
    {
        var plugin = new MistralPlugin();
        var config = new LlmBackendConfig
        {
            Name = "Test",
            BaseUrl = "https://api.mistral.ai",
            ApiKey = "test-key"
        };

        var backend = plugin.CreateBackend(
            "Mistral",
            config,
            Mock.Of<ILoggerFactory>(),
            Mock.Of<IHttpClientFactory>());

        Assert.NotNull(backend);
        Assert.Equal("Test", backend.Name);
    }
}
```

### Integration Tests

```csharp
[Fact]
public async Task MistralBackend_CompletesSuccessfully()
{
    // Arrange
    var config = new LlmBackendConfig
    {
        Name = "Mistral-Test",
        BaseUrl = "https://api.mistral.ai",
        ApiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY"),
        ModelName = "mistral-small-latest"
    };

    var httpClient = new HttpClient { BaseAddress = new Uri(config.BaseUrl) };
    var backend = new MistralLlmBackend(
        config,
        Mock.Of<ILogger<MistralLlmBackend>>(),
        httpClient);

    // Act
    var request = new LlmRequest { Prompt = "Say hello" };
    var response = await backend.CompleteAsync(request);

    // Assert
    Assert.True(response.Success);
    Assert.NotEmpty(response.Content);
}
```

## Best Practices

### 1. Error Handling

Always wrap API calls in try-catch and use proper logging:

```csharp
try
{
    var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
    // ...
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "HTTP error calling {Backend}", Name);
    RecordFailure(ex.Message);
    return CreateErrorResponse("HttpError", ex.Message);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error calling {Backend}", Name);
    RecordFailure(ex.Message);
    return CreateErrorResponse("Exception", ex.Message);
}
```

### 2. Configuration Validation

Validate configuration in the backend constructor:

```csharp
public MistralLlmBackend(LlmBackendConfig config, ...)
    : base(config, logger, httpClient)
{
    if (string.IsNullOrEmpty(config.BaseUrl))
    {
        throw new ArgumentException("BaseUrl is required", nameof(config));
    }

    if (string.IsNullOrEmpty(config.ApiKey))
    {
        _logger.LogWarning("No API key provided for Mistral backend");
    }

    ConfigureHttpClient();
}
```

### 3. Metrics Tracking

Always call RecordSuccess() and RecordFailure():

```csharp
var stopwatch = Stopwatch.StartNew();
try
{
    // Make API call
    var response = await CallApi();

    stopwatch.Stop();
    RecordSuccess(stopwatch.ElapsedMilliseconds);  // ✓ Record success

    return CreateSuccessResponse(response);
}
catch (Exception ex)
{
    stopwatch.Stop();
    RecordFailure(ex.Message);  // ✓ Record failure
    return CreateErrorResponse("Error", ex.Message);
}
```

### 4. Thread Safety

Use thread-safe operations when accessing shared state:

```csharp
private readonly ConcurrentDictionary<string, object> _cache = new();

public void CacheResponse(string key, object value)
{
    _cache.AddOrUpdate(key, value, (k, v) => value);
}
```

### 5. Resource Cleanup

Dispose of resources properly if needed:

```csharp
public class MyBackend : BaseLlmBackend, IDisposable
{
    private readonly SomeResource _resource;

    public void Dispose()
    {
        _resource?.Dispose();
    }
}
```

### 6. Documentation

Document your plugin thoroughly:

```csharp
/// <summary>
/// Mistral AI backend implementation supporting Mistral Large, Medium, and Small models
/// </summary>
/// <remarks>
/// Requires API key from https://console.mistral.ai/
/// Supports both completion and chat endpoints
/// Default model: mistral-large-latest
/// </remarks>
public class MistralLlmBackend : BaseLlmBackend
{
    // ...
}
```

## Plugin Distribution

### NuGet Package

Create a NuGet package for easy distribution:

```xml
<PropertyGroup>
  <PackageId>MyCompany.LlmBackend.MistralPlugin</PackageId>
  <Version>1.0.0</Version>
  <Authors>Your Company</Authors>
  <Description>Mistral AI plugin for mostlylucid.llmbackend</Description>
  <PackageTags>LLM;Mistral;AI;Plugin</PackageTags>
</PropertyGroup>
```

```bash
dotnet pack -c Release
```

### Installation

Users can install via NuGet and the plugin will be automatically discovered:

```bash
dotnet add package MyCompany.LlmBackend.MistralPlugin
```

## Troubleshooting

### Plugin Not Loading

Check logs for errors:
```
[Warning] Plugin directory plugins does not exist, skipping plugin loading
[Error] Failed to load plugin from plugins/MyPlugin.dll: ...
```

Solutions:
- Ensure plugin directory exists
- Check DLL is in the correct location
- Verify plugin implements ILlmBackendPlugin
- Check for missing dependencies

### Backend Not Creating

```
Backend type Mistral is not supported. If this is a plugin backend, ensure CustomBackendType is set and the plugin is loaded.
```

Solutions:
- Set `CustomBackendType` in configuration
- Ensure plugin is loaded before creating backends
- Check `SupportedBackendTypes` matches configuration

### Runtime Errors

Enable detailed logging:
```json
{
  "Logging": {
    "LogLevel": {
      "Mostlyucid.LlmBackend": "Debug",
      "MyCompany.LlmBackend.MistralPlugin": "Debug"
    }
  }
}
```

## Summary

Creating plugins for mostlylucid.llmbackend is straightforward:

1. ✅ Create a class library
2. ✅ Reference mostlylucid.llmbackend
3. ✅ Implement ILlmBackend (inherit from BaseLlmBackend)
4. ✅ Implement ILlmBackendPlugin
5. ✅ Build and deploy DLL
6. ✅ Configure and use!

This allows you to extend the library with any LLM provider while maintaining compatibility with all existing features like failover, retries, caching, and monitoring.
