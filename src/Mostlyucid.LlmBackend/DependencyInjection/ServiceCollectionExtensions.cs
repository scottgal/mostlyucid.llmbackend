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

        // Register plugin loader
        services.AddSingleton<LlmPluginLoader>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<LlmPluginLoader>();
            var pluginLoader = new LlmPluginLoader(logger);

            var settings = configuration.GetSection(sectionName).Get<LlmSettings>();
            if (settings?.Plugins?.Enabled == true && settings.Plugins.LoadOnStartup)
            {
                var pluginDirectory = settings.Plugins.PluginDirectory;

                // Make path absolute if it's relative
                if (!Path.IsPathRooted(pluginDirectory))
                {
                    pluginDirectory = Path.Combine(AppContext.BaseDirectory, pluginDirectory);
                }

                if (Directory.Exists(pluginDirectory))
                {
                    pluginLoader.LoadPluginsFromDirectory(pluginDirectory);
                }
                else
                {
                    logger.LogWarning(
                        "Plugin directory {PluginDirectory} does not exist, skipping plugin loading",
                        pluginDirectory);
                }

                // Load specific plugins if configured
                if (settings.Plugins.SpecificPlugins != null)
                {
                    foreach (var pluginPath in settings.Plugins.SpecificPlugins)
                    {
                        var fullPath = Path.IsPathRooted(pluginPath)
                            ? pluginPath
                            : Path.Combine(AppContext.BaseDirectory, pluginPath);

                        if (File.Exists(fullPath))
                        {
                            pluginLoader.LoadPluginFromAssembly(fullPath);
                        }
                        else
                        {
                            logger.LogWarning("Plugin file {PluginPath} not found", fullPath);
                        }
                    }
                }
            }

            return pluginLoader;
        });

        // Register factory with plugin support
        services.AddSingleton<LlmBackendFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var httpClientFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            var pluginLoader = sp.GetRequiredService<LlmPluginLoader>();
            var settings = configuration.GetSection(sectionName).Get<LlmSettings>();

            return new LlmBackendFactory(loggerFactory, httpClientFactory, pluginLoader, settings?.Telemetry);
        });

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
