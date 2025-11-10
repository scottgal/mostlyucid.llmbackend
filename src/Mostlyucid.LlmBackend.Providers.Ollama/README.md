# Mostlyucid.LlmBackend.Providers.Ollama

Ollama provider for Mostlyucid.LlmBackend library. Supports locally-hosted models.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.Ollama
```

## Prerequisites

Install and run [Ollama](https://ollama.ai) or [LM Studio](https://lmstudio.ai) locally.

## Usage

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.Ollama;
    options.Ollama = new OllamaSettings
    {
        Model = "llama2",
        BaseUrl = "http://localhost:11434"
    };
});
```

## Supported Models

Works with any model available in Ollama:
- Llama 2 (llama2)
- Llama 3 (llama3)
- Mistral (mistral)
- Mixtral (mixtral)
- Phi (phi)
- And many more...

## Features

- Full streaming support
- Local model execution (no API costs)
- Privacy-focused (data stays local)
- Compatible with LM Studio
- Custom base URL support
- Token usage tracking

## Configuration Notes

- `BaseUrl` defaults to http://localhost:11434 (Ollama default)
- For LM Studio, use http://localhost:1234
- Ensure Ollama is running before making requests
- Pull models using `ollama pull <model-name>` command

## License

MIT
