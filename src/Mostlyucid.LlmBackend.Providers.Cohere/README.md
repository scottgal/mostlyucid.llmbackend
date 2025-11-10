# Mostlyucid.LlmBackend.Providers.Cohere

Cohere provider for Mostlyucid.LlmBackend library.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.Cohere
```

## Usage

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.Cohere;
    options.Cohere = new CohereSettings
    {
        ApiKey = "your-api-key",
        Model = "command-r-plus",
        BaseUrl = "https://api.cohere.com"
    };
});
```

## Supported Models

- Command R Plus (command-r-plus)
- Command R (command-r)
- Command (command)
- Command Light (command-light)

## Features

- Full streaming support
- Chat history support
- Token usage tracking
- Cost estimation
- Custom temperature and parameters

## License

MIT
