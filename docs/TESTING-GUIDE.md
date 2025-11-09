# Testing Guide

This guide shows how to test code that uses the mostlylucid.llmbackend library using the provided fake implementations.

## Table of Contents
- [Overview](#overview)
- [Fake Backend](#fake-backend)
- [Fake Prompt Builder](#fake-prompt-builder)
- [Unit Testing Examples](#unit-testing-examples)
- [Integration Testing](#integration-testing)
- [Testing Strategies](#testing-strategies)

## Overview

The library provides fake/mock implementations for all major integration points:

- **FakeLlmBackend** - Test double for ILlmBackend
- **FakePromptBuilder** - Test double for IPromptBuilder

These fakes allow you to:
- ✅ Test without calling real APIs (no costs, no network)
- ✅ Control responses and simulate failures
- ✅ Verify request parameters
- ✅ Test failover and retry logic
- ✅ Performance test without API limits

## Fake Backend

### Basic Usage

```csharp
using Mostlyucid.LlmBackend.Testing;
using Mostlyucid.LlmBackend.Models;

// Create fake backend
var fake = new FakeLlmBackend("test-backend")
{
    ResponseText = "This is a test response",
    ModelUsed = "test-model-1",
    LatencyMs = 100  // Simulate 100ms latency
};

// Use it
var request = new LlmRequest { Prompt = "Hello" };
var response = await fake.CompleteAsync(request);

// Verify
Assert.True(response.Success);
Assert.Equal("This is a test response", response.Content);
Assert.Equal("test-backend", response.BackendUsed);
Assert.Equal(1, fake.RequestCount);
```

### Response Templates

Use template variables in responses:

```csharp
var fake = new FakeLlmBackend
{
    ResponseText = "You said: {Prompt} (Request #{RequestCount} to {Name})"
};

var response = await fake.CompleteAsync(
    new LlmRequest { Prompt = "Hello" });

// Output: "You said: Hello (Request #1 to FakeBackend)"
```

### Simulating Failures

```csharp
var fake = new FakeLlmBackend
{
    SimulateFailure = true,
    FailureMessage = "API rate limit exceeded"
};

var response = await fake.CompleteAsync(request);

Assert.False(response.Success);
Assert.Equal("API rate limit exceeded", response.ErrorMessage);
```

### Simulating Unavailability

```csharp
var fake = new FakeLlmBackend
{
    IsAvailable = false
};

var isAvailable = await fake.IsAvailableAsync();
Assert.False(isAvailable);

// Useful for testing failover logic
```

### Controlling Token Counts

```csharp
var fake = new FakeLlmBackend
{
    PromptTokens = 150,
    CompletionTokens = 300
};

var response = await fake.CompleteAsync(request);

Assert.Equal(150, response.PromptTokens);
Assert.Equal(300, response.CompletionTokens);
Assert.Equal(450, response.TotalTokens);
```

### Inspecting Requests

```csharp
var fake = new FakeLlmBackend();

await fake.CompleteAsync(new LlmRequest { Prompt = "Test 1" });
await fake.CompleteAsync(new LlmRequest { Prompt = "Test 2" });

// Verify request count
Assert.Equal(2, fake.RequestCount);

// Inspect last request
Assert.NotNull(fake.LastRequest);
Assert.Equal("Test 2", fake.LastRequest.Prompt);

// Reset for next test
fake.Reset();
Assert.Equal(0, fake.RequestCount);
```

## Fake Prompt Builder

### Basic Usage

```csharp
using Mostlyucid.LlmBackend.Testing;

var fake = new FakePromptBuilder
{
    PromptTemplate = "Task: {userMessage}\nInstructions: {instructions}"
};

var prompt = fake.BuildPrompt("Translate this", new PromptContext
{
    TemplateVariables = new Dictionary<string, string>
    {
        ["instructions"] = "Be concise"
    }
});

// Output: "Task: Translate this\nInstructions: Be concise"
Assert.Equal(1, fake.BuildPromptCallCount);
```

### Testing Conversation History

```csharp
var fake = new FakePromptBuilder
{
    IncludeHistory = true,
    MaxHistoryMessages = 3
};

// Add to memory
fake.AddToMemory("user", "First message");
fake.AddToMemory("assistant", "First response");
fake.AddToMemory("user", "Second message");

// Build prompt - includes history
var prompt = fake.BuildPrompt("Third message");

Assert.Contains("First message", prompt);
Assert.Contains("First response", prompt);
Assert.Contains("Second message", prompt);
Assert.Contains("Third message", prompt);
```

### Verifying Calls

```csharp
var fake = new FakePromptBuilder();

fake.BuildPrompt("Test 1");
fake.BuildChatRequest("Test 2");
fake.AddToMemory("user", "Test 3");
fake.ClearMemory();

// Verify call counts
Assert.Equal(1, fake.BuildPromptCallCount);
Assert.Equal(1, fake.BuildChatRequestCallCount);
Assert.Equal(1, fake.AddToMemoryCallCount);
Assert.Equal(1, fake.ClearMemoryCallCount);

// Inspect last call
Assert.Equal("Test 2", fake.LastUserMessage);
```

## Unit Testing Examples

### Testing a Translation Service

```csharp
using Xunit;
using Mostlyucid.LlmBackend.Testing;
using Mostlyucid.LlmBackend.Models;

public class TranslationServiceTests
{
    [Fact]
    public async Task Translate_UsesCorrectPrompt()
    {
        // Arrange
        var fakeBackend = new FakeLlmBackend
        {
            ResponseText = "Bonjour"
        };

        var fakePromptBuilder = new FakePromptBuilder
        {
            PromptTemplate = "Translate to {targetLanguage}: {userMessage}"
        };

        var service = new TranslationService(fakeBackend, fakePromptBuilder);

        // Act
        var result = await service.TranslateAsync(
            "Hello",
            "English",
            "French");

        // Assert
        Assert.Equal("Bonjour", result);
        Assert.Equal(1, fakeBackend.RequestCount);
        Assert.Equal(1, fakePromptBuilder.BuildPromptCallCount);

        // Verify prompt was built correctly
        var lastContext = fakePromptBuilder.LastContext;
        Assert.Equal("French", lastContext.TemplateVariables["targetLanguage"]);
    }

    [Fact]
    public async Task Translate_HandlesFailureGracefully()
    {
        // Arrange
        var fakeBackend = new FakeLlmBackend
        {
            SimulateFailure = true,
            FailureMessage = "Service unavailable"
        };

        var service = new TranslationService(fakeBackend, null);

        // Act & Assert
        await Assert.ThrowsAsync<TranslationException>(
            () => service.TranslateAsync("Hello", "en", "fr"));
    }
}
```

### Testing Retry Logic

```csharp
[Fact]
public async Task Service_RetriesOnFailure()
{
    // Arrange
    var fakeBackend = new FakeLlmBackend();
    var service = new MyLlmService(fakeBackend, maxRetries: 3);

    // Fail first 2 attempts, succeed on 3rd
    int attempt = 0;
    fakeBackend.SimulateFailure = true;

    // This requires a more sophisticated fake that can change behavior per call
    // Alternative: Use a real service with multiple fake backends in failover

    // Act
    // Configure failover backends...
    var result = await service.CompleteAsync(request);

    // Assert
    Assert.True(result.Success);
    // Verify retries occurred
}
```

### Testing Failover

```csharp
[Fact]
public async Task Service_FailsOverToSecondaryBackend()
{
    // Arrange - Primary backend unavailable
    var primaryBackend = new FakeLlmBackend("primary")
    {
        IsAvailable = false
    };

    // Secondary backend available
    var secondaryBackend = new FakeLlmBackend("secondary")
    {
        ResponseText = "Response from secondary"
    };

    var backends = new[] { primaryBackend, secondaryBackend };
    var service = new LlmService(backends, BackendSelectionStrategy.Failover);

    // Act
    var response = await service.CompleteAsync(request);

    // Assert
    Assert.True(response.Success);
    Assert.Equal("secondary", response.BackendUsed);
    Assert.Equal(0, primaryBackend.RequestCount);  // Wasn't called
    Assert.Equal(1, secondaryBackend.RequestCount);  // Was called
}
```

### Testing Token Limits

```csharp
[Fact]
public async Task Service_RespectsTokenLimits()
{
    // Arrange
    var fakeBackend = new FakeLlmBackend
    {
        PromptTokens = 4000,
        CompletionTokens = 1000
    };

    var service = new MyService(fakeBackend, maxTotalTokens: 4096);

    // Act
    var response = await service.CompleteAsync(request);

    // Assert
    Assert.True(response.TotalTokens <= 4096);
}
```

## Integration Testing

### Testing Against Multiple Fakes

```csharp
public class IntegrationTests
{
    [Fact]
    public async Task EndToEnd_TranslationWorkflow()
    {
        // Arrange - Set up entire dependency chain with fakes
        var fakeBackend = new FakeLlmBackend
        {
            ResponseText = "Translated: {Prompt}",
            LatencyMs = 50  // Simulate realistic latency
        };

        var fakePromptBuilder = new FakePromptBuilder
        {
            PromptTemplate = "Translate to {targetLanguage}: {userMessage}"
        };

        var fakeMemory = new InMemoryContextMemory();

        var service = new TranslationService(
            fakeBackend,
            fakePromptBuilder,
            fakeMemory);

        // Act - Execute workflow
        var result1 = await service.TranslateAsync("Hello", "en", "fr");
        var result2 = await service.TranslateAsync("Goodbye", "en", "fr");

        // Assert - Verify entire flow
        Assert.Contains("Translated", result1);
        Assert.Contains("Translated", result2);

        // Verify all components were used correctly
        Assert.Equal(2, fakeBackend.RequestCount);
        Assert.Equal(2, fakePromptBuilder.BuildPromptCallCount);

        // Verify memory was used
        var memory = fakeMemory.GetAllMessages();
        Assert.Equal(4, memory.Count);  // 2 requests + 2 responses
    }
}
```

### Performance Testing

```csharp
[Fact]
public async Task Service_HandlesHighLoad()
{
    // Arrange
    var fakeBackend = new FakeLlmBackend
    {
        LatencyMs = 10,  // Very fast response
        ResponseText = "Response"
    };

    var service = new MyService(fakeBackend);

    // Act - Simulate load
    var tasks = Enumerable.Range(0, 100)
        .Select(_ => service.CompleteAsync(
            new LlmRequest { Prompt = "Test" }));

    var results = await Task.WhenAll(tasks);

    // Assert
    Assert.Equal(100, results.Length);
    Assert.All(results, r => Assert.True(r.Success));
    Assert.Equal(100, fakeBackend.RequestCount);
}
```

## Testing Strategies

### Strategy 1: Per-Test Fakes

Create fresh fakes for each test:

```csharp
[Fact]
public async Task Test1()
{
    var fake = new FakeLlmBackend { ResponseText = "Response 1" };
    // Test...
}

[Fact]
public async Task Test2()
{
    var fake = new FakeLlmBackend { ResponseText = "Response 2" };
    // Test...
}
```

**Pros:** Complete isolation
**Cons:** More boilerplate

### Strategy 2: Shared Fakes with Reset

Share fakes across tests, reset between tests:

```csharp
public class MyTests
{
    private readonly FakeLlmBackend _fakeBackend = new();

    [Fact]
    public async Task Test1()
    {
        _fakeBackend.ResponseText = "Response 1";
        // Test...
        _fakeBackend.Reset();
    }

    [Fact]
    public async Task Test2()
    {
        _fakeBackend.ResponseText = "Response 2";
        // Test...
        _fakeBackend.Reset();
    }
}
```

**Pros:** Less boilerplate
**Cons:** Risk of test pollution if reset is forgotten

### Strategy 3: Test Fixtures

Use xUnit fixtures for complex setups:

```csharp
public class LlmTestFixture : IDisposable
{
    public FakeLlmBackend Backend { get; }
    public FakePromptBuilder PromptBuilder { get; }

    public LlmTestFixture()
    {
        Backend = new FakeLlmBackend();
        PromptBuilder = new FakePromptBuilder();
    }

    public void Dispose()
    {
        Backend.Reset();
        PromptBuilder.Reset();
    }
}

public class MyTests : IClassFixture<LlmTestFixture>
{
    private readonly LlmTestFixture _fixture;

    public MyTests(LlmTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test()
    {
        // Use _fixture.Backend and _fixture.PromptBuilder
    }
}
```

**Pros:** Reusable, automatic cleanup
**Cons:** Shared state across tests

### Strategy 4: Builder Pattern

Create a test builder for complex scenarios:

```csharp
public class LlmTestBuilder
{
    private readonly FakeLlmBackend _backend = new();

    public LlmTestBuilder WithResponse(string response)
    {
        _backend.ResponseText = response;
        return this;
    }

    public LlmTestBuilder WithFailure(string message)
    {
        _backend.SimulateFailure = true;
        _backend.FailureMessage = message;
        return this;
    }

    public LlmTestBuilder WithLatency(long ms)
    {
        _backend.LatencyMs = ms;
        return this;
    }

    public FakeLlmBackend Build() => _backend;
}

// Usage
var backend = new LlmTestBuilder()
    .WithResponse("Test response")
    .WithLatency(100)
    .Build();
```

**Pros:** Fluent, readable
**Cons:** Extra code to maintain

## Best Practices

### 1. Test One Thing

Each test should verify one behavior:

```csharp
// Good - Tests one thing
[Fact]
public async Task CompleteAsync_ReturnsExpectedResponse()
{
    var fake = new FakeLlmBackend { ResponseText = "Test" };
    var result = await service.CompleteAsync(request);
    Assert.Equal("Test", result.Content);
}

// Bad - Tests multiple things
[Fact]
public async Task CompleteAsync_WorksCorrectly()
{
    // Tests response, token counts, latency, failure handling all in one
}
```

### 2. Use Descriptive Test Names

```csharp
// Good
[Fact]
public async Task CompleteAsync_WhenBackendFails_ThrowsException()

// Bad
[Fact]
public async Task Test1()
```

### 3. Arrange-Act-Assert

Structure tests clearly:

```csharp
[Fact]
public async Task Test()
{
    // Arrange
    var fake = new FakeLlmBackend { ResponseText = "Test" };

    // Act
    var result = await service.CompleteAsync(request);

    // Assert
    Assert.Equal("Test", result.Content);
}
```

### 4. Reset State

Always reset fakes between tests:

```csharp
public void Dispose()
{
    _fakeBackend.Reset();
    _fakePromptBuilder.Reset();
}
```

### 5. Test Error Paths

Don't just test happy paths:

```csharp
[Theory]
[InlineData("Rate limit", typeof(RateLimitException))]
[InlineData("Timeout", typeof(TimeoutException))]
[InlineData("Auth failed", typeof(AuthException))]
public async Task Service_HandlesErrors(string error, Type exceptionType)
{
    _fakeBackend.SimulateFailure = true;
    _fakeBackend.FailureMessage = error;

    await Assert.ThrowsAsync(exceptionType,
        () => service.CompleteAsync(request));
}
```

## Summary

The fake implementations provide:

- ✅ **Fast tests** - No network calls
- ✅ **Reliable tests** - Deterministic behavior
- ✅ **Cost-free tests** - No API charges
- ✅ **Complete control** - Simulate any scenario
- ✅ **Easy verification** - Inspect all interactions

Use them to build a comprehensive test suite that gives you confidence
when deploying code that depends on LLM backends!
