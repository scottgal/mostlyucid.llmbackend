# Mostlyucid.LlmBackend.Providers.Anthropic

Anthropic Claude provider for Mostlyucid.LlmBackend library.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.Anthropic
```

## Usage

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.Anthropic;
    options.Anthropic = new AnthropicSettings
    {
        ApiKey = "your-api-key",
        Model = "claude-3-5-sonnet-20241022",
        BaseUrl = "https://api.anthropic.com"
    };
});
```

## Supported Models

- Claude 3 Opus (claude-3-opus-20240229)
- Claude 3 Sonnet (claude-3-sonnet-20240229)
- Claude 3.5 Sonnet (claude-3-5-sonnet-20241022)
- Claude 3 Haiku (claude-3-haiku-20240307)

## Features

- Full streaming support
- System message support
- Vision capabilities (for supported models)
- Token usage tracking
- Cost estimation
- Custom API version support

## License

MIT
