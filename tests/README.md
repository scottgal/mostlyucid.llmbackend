# Mostlyucid.LlmBackend Tests

This directory contains comprehensive unit and integration tests for the Mostlyucid.LlmBackend library.

## Test Structure

```
tests/
└── Mostlyucid.LlmBackend.Tests/
    ├── Services/
    │   ├── LlmServiceTests.cs          # Core service tests (selection strategies, failover)
    │   ├── BaseLlmBackendTests.cs      # Base backend tests (budget, health tracking)
    │   ├── LlmBackendFactoryTests.cs   # Factory pattern tests
    │   └── PromptBuilderTests.cs       # Prompt building tests
    └── Models/
        └── ModelTests.cs               # Model/DTO tests
```

## Test Coverage

The test suite covers:

### Core Services (90%+ coverage target)
- **LlmService**: All backend selection strategies (Failover, Round Robin, Lowest Latency, Random, Simultaneous)
- **BaseLlmBackend**: Budget tracking, health management, metrics recording
- **LlmBackendFactory**: Backend instantiation for all provider types

### Models (95%+ coverage target)
- **LlmRequest/LlmResponse**: Property initialization and behavior
- **ChatMessage/ChatRequest**: Chat-specific functionality

### Prompt Building (85%+ coverage target)
- **DefaultPromptBuilder**: Template variables, context, examples, history
- **TranslationPromptBuilder**: Translation-specific prompt generation

## Running Tests

### Using .NET CLI

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test class
dotnet test --filter "FullyQualifiedName~LlmServiceTests"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Using Visual Studio

1. Open the solution in Visual Studio
2. Open Test Explorer (Test > Test Explorer)
3. Click "Run All" or select specific tests

### Using Rider

1. Open the solution in Rider
2. Right-click on the test project or individual test files
3. Select "Run Unit Tests" or "Debug Unit Tests"

## Test Frameworks and Libraries

- **xUnit**: Primary testing framework
- **FluentAssertions**: Readable assertions
- **Moq**: Mocking framework for dependencies
- **Coverlet**: Code coverage tool

## Writing New Tests

### Test Class Template

```csharp
using FluentAssertions;
using Mostlyucid.LlmBackend.Services;

namespace Mostlyucid.LlmBackend.Tests.Services;

public class MyComponentTests
{
    [Fact]
    public void MyMethod_WithValidInput_ShouldReturnExpectedResult()
    {
        // Arrange
        var component = new MyComponent();

        // Act
        var result = component.MyMethod("input");

        // Assert
        result.Should().Be("expected");
    }
}
```

### Best Practices

1. **Follow AAA Pattern**: Arrange, Act, Assert
2. **One Assertion Per Test**: Keep tests focused
3. **Use Descriptive Names**: `MethodName_Scenario_ExpectedBehavior`
4. **Test Edge Cases**: Null values, empty collections, boundary conditions
5. **Use FluentAssertions**: More readable than xUnit assertions
6. **Mock External Dependencies**: Use `FakeLlmBackend` for testing

## Test Utilities

The library provides test utilities in the `Testing` namespace:

### FakeLlmBackend

```csharp
var fakeBackend = new FakeLlmBackend
{
    Name = "TestBackend"
};

// Configure response
fakeBackend.SetResponse(new LlmResponse
{
    Success = true,
    Text = "Test response"
});

// Simulate failures
fakeBackend.SetException(new InvalidOperationException("Test error"));

// Simulate latency
fakeBackend.SetLatency(TimeSpan.FromMilliseconds(500));
```

### FakePromptBuilder

```csharp
var fakeBuilder = new FakePromptBuilder();
fakeBuilder.SetPrompt("Generated prompt");

var prompt = fakeBuilder.BuildPrompt("User message");
// Returns "Generated prompt"
```

## Code Coverage Goals

| Component | Target Coverage |
|-----------|----------------|
| LlmService | 90% |
| BaseLlmBackend | 90% |
| LlmBackendFactory | 85% |
| Models | 95% |
| Prompt Builders | 85% |
| Overall Project | 85% |

## Continuous Integration

Tests are automatically run on:
- Every commit to feature branches
- Pull requests to main
- Nightly builds

CI configuration ensures:
- All tests pass
- Code coverage meets minimum thresholds
- No compilation warnings

## Troubleshooting

### Tests Fail Locally But Pass in CI

- Ensure you're using the same .NET SDK version
- Check for environment-specific dependencies
- Clear `bin` and `obj` folders: `dotnet clean`

### Flaky Tests

- Avoid `Task.Delay()` for timing; use `Stopwatch` assertions with tolerances
- Use `BeCloseTo()` for time-based assertions
- Mock external dependencies properly

### Coverage Not Generated

```bash
# Install coverlet.msbuild
dotnet add package coverlet.msbuild

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## Future Test Additions

Planned test coverage expansions:

- [ ] Plugin system tests (LlmPluginLoader, PromptBuilderPluginLoader)
- [ ] Configuration validation tests
- [ ] Integration tests with real backends (requires API keys)
- [ ] Performance/load tests
- [ ] Concurrent request handling tests

## Contributing

When adding new features:

1. Write tests first (TDD approach recommended)
2. Ensure all existing tests pass
3. Add tests for edge cases
4. Update this README if adding new test categories
5. Maintain or improve code coverage percentages

## License

Tests are part of the Mostlyucid.LlmBackend project and share the same license.
