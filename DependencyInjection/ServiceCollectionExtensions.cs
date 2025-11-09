using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlyucid.LlmBackend.Configuration;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Services;

namespace Mostlyucid.LlmBackend.DependencyInjection;

/// <summary>
/// Extension methods for configuring LLM backend services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add LLM backend services to the service collection
    /// </summary>
    public static IServiceCollection AddLlmBackend(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = LlmSettings.SectionName)
    {
        // Register configuration
        services.Configure<LlmSettings>(configuration.GetSection(sectionName));

        // Register HTTP client factory for backends
        services.AddHttpClient();

        // Register factory
        services.AddSingleton<LlmBackendFactory>();

        // Register context memory
        services.AddSingleton<IContextMemory, InMemoryContextMemory>();

        // Register prompt builders
        services.AddTransient<IPromptBuilder, DefaultPromptBuilder>();
        services.AddTransient<TranslationPromptBuilder>();

        // Register backend instances
        services.AddSingleton<IEnumerable<ILlmBackend>>(sp =>
        {
            var factory = sp.GetRequiredService<LlmBackendFactory>();
            var settings = configuration.GetSection(sectionName).Get<LlmSettings>();

            if (settings?.Backends == null || settings.Backends.Count == 0)
            {
                return new List<ILlmBackend>();
            }

            return factory.CreateBackends(settings.Backends);
        });

        // Register main service
        services.AddSingleton<ILlmService, LlmService>();

        return services;
    }

    /// <summary>
    /// Add LLM backend services with manual configuration
    /// </summary>
    public static IServiceCollection AddLlmBackend(
        this IServiceCollection services,
        Action<LlmSettings> configureSettings)
    {
        services.Configure(configureSettings);
        services.AddHttpClient();
        services.AddSingleton<LlmBackendFactory>();
        services.AddSingleton<IContextMemory, InMemoryContextMemory>();
        services.AddTransient<IPromptBuilder, DefaultPromptBuilder>();
        services.AddTransient<TranslationPromptBuilder>();

        services.AddSingleton<IEnumerable<ILlmBackend>>(sp =>
        {
            var factory = sp.GetRequiredService<LlmBackendFactory>();
            var settings = new LlmSettings();
            configureSettings(settings);

            if (settings.Backends == null || settings.Backends.Count == 0)
            {
                return new List<ILlmBackend>();
            }

            return factory.CreateBackends(settings.Backends);
        });

        services.AddSingleton<ILlmService, LlmService>();

        return services;
    }

    /// <summary>
    /// Add a single LLM backend
    /// </summary>
    public static IServiceCollection AddLlmBackend(
        this IServiceCollection services,
        LlmBackendType backendType,
        Action<LlmBackendConfig> configureBackend)
    {
        var config = new LlmBackendConfig
        {
            Type = backendType,
            Name = backendType.ToString()
        };

        configureBackend(config);

        return services.AddLlmBackend(settings =>
        {
            settings.Backends = new List<LlmBackendConfig> { config };
        });
    }
}
