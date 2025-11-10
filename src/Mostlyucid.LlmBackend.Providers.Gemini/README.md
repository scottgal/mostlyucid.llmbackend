# Mostlyucid.LlmBackend.Providers.Gemini

Google Gemini provider for Mostlyucid.LlmBackend library.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.Gemini
```

## Usage

### AI Studio (API Key)

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.Gemini;
    options.Gemini = new GeminiSettings
    {
        ApiKey = "your-api-key",
        Model = "gemini-pro",
        BaseUrl = "https://generativelanguage.googleapis.com"
    };
});
```

### Vertex AI (Service Account)

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.Gemini;
    options.Gemini = new GeminiSettings
    {
        ApiKey = "your-service-account-token",
        Model = "gemini-pro",
        ProjectId = "your-gcp-project-id",
        Location = "us-central1"
    };
});
```

## Supported Models

- Gemini Pro (gemini-pro)
- Gemini Pro Vision (gemini-pro-vision)
- Gemini 1.5 Pro (gemini-1.5-pro)
- Gemini 1.5 Flash (gemini-1.5-flash)

## Features

- AI Studio and Vertex AI endpoint support
- Full streaming support
- Vision capabilities (for supported models)
- Safety settings configuration
- Token usage tracking
- Cost estimation

## License

MIT
