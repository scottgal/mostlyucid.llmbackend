# Mostlyucid.LlmBackend.Providers.AzureOpenAI

Azure OpenAI provider for Mostlyucid.LlmBackend library.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.AzureOpenAI
```

## Usage

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.AzureOpenAI;
    options.AzureOpenAI = new AzureOpenAISettings
    {
        ApiKey = "your-azure-api-key",
        DeploymentName = "gpt-4-deployment",
        BaseUrl = "https://your-resource.openai.azure.com",
        ApiVersion = "2024-02-15-preview"
    };
});
```

## Supported Models

- Azure-hosted GPT-4 and GPT-4 variants
- Azure-hosted GPT-3.5-turbo and variants
- All Azure OpenAI chat completion models

## Features

- Deployment name support
- API version management
- Full streaming support
- Function calling support
- Custom resource endpoint support
- Token usage tracking
- Cost estimation

## Configuration Notes

- `BaseUrl` should be your Azure OpenAI resource endpoint (e.g., https://your-resource.openai.azure.com)
- `DeploymentName` is the name you assigned to your model deployment in Azure
- `ApiVersion` should match your Azure OpenAI API version (e.g., 2024-02-15-preview)

## License

MIT
