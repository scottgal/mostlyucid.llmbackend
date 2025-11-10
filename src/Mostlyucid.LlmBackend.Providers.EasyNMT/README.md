# Mostlyucid.LlmBackend.Providers.EasyNMT

EasyNMT translation provider for Mostlyucid.LlmBackend library. Specialized backend for neural machine translation.

## Installation

```bash
dotnet add package Mostlyucid.LlmBackend
dotnet add package Mostlyucid.LlmBackend.Providers.EasyNMT
```

## Prerequisites

Install and run [EasyNMT](https://github.com/UKPLab/EasyNMT) locally or use a hosted instance.

## Usage

```csharp
services.AddLlmBackend(options =>
{
    options.DefaultBackend = LlmBackendType.EasyNMT;
    options.EasyNMT = new EasyNMTSettings
    {
        BaseUrl = "http://localhost:24080"
    };
});
```

## Features

- High-quality neural machine translation
- Multiple language pair support
- Specialized translation models
- Local execution support
- Custom base URL configuration

## Configuration Notes

- `BaseUrl` should point to your EasyNMT instance
- Default port is typically 24080
- Supports various translation models (mBART, M2M-100, etc.)

## Use Cases

- Document translation
- Multi-language content generation
- Translation-specific workflows
- Offline translation needs

## License

MIT
