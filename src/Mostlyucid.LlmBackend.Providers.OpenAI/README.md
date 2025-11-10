# Mostlyucid.LlmBackend.Providers.OpenAI

OpenAI provider for Mostlyucid.LlmBackend library.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.OpenAI
```

## Usage

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.OpenAI;
    options.OpenAI = new OpenAISettings
    {
        ApiKey = "your-api-key",
        Model = "gpt-4",
        BaseUrl = "https://api.openai.com"
    };
});
```

## Supported Models

- GPT-4 and GPT-4 variants (gpt-4, gpt-4-turbo, gpt-4-32k)
- GPT-3.5-turbo and variants
- All OpenAI chat completion models

## Features

- Full streaming support
- Function calling support
- Organization ID support
- Custom base URL support
- Token usage tracking
- Cost estimation

## License

MIT
