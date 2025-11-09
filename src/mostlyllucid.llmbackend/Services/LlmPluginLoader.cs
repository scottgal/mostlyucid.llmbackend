using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Interfaces;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Loads LLM backend plugins from assemblies
/// </summary>
public class LlmPluginLoader
{
    private readonly ILogger<LlmPluginLoader> _logger;
    private readonly List<ILlmBackendPlugin> _plugins = new();
    private readonly Dictionary<string, ILlmBackendPlugin> _pluginsByType = new();

    public LlmPluginLoader(ILogger<LlmPluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    public IReadOnlyList<ILlmBackendPlugin> Plugins => _plugins.AsReadOnly();

    /// <summary>
    /// Load plugins from a directory
    /// </summary>
    /// <param name="pluginDirectory">Directory containing plugin DLLs</param>
    public void LoadPluginsFromDirectory(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogWarning("Plugin directory {Directory} does not exist", pluginDirectory);
            return;
        }

        _logger.LogInformation("Loading plugins from {Directory}", pluginDirectory);

        var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                LoadPluginFromAssembly(dllPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Path}", dllPath);
            }
        }

        _logger.LogInformation("Loaded {Count} plugins", _plugins.Count);
    }

    /// <summary>
    /// Load a plugin from a specific assembly file
    /// </summary>
    /// <param name="assemblyPath">Path to the plugin assembly</param>
    public void LoadPluginFromAssembly(string assemblyPath)
    {
        _logger.LogDebug("Attempting to load plugin from {Path}", assemblyPath);

        // Load the assembly
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

        // Find types that implement ILlmBackendPlugin
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(ILlmBackendPlugin).IsAssignableFrom(t) &&
                       !t.IsInterface &&
                       !t.IsAbstract);

        foreach (var pluginType in pluginTypes)
        {
            try
            {
                // Create an instance of the plugin
                var plugin = Activator.CreateInstance(pluginType) as ILlmBackendPlugin;

                if (plugin == null)
                {
                    _logger.LogWarning("Failed to create instance of plugin type {Type}", pluginType.Name);
                    continue;
                }

                // Validate the plugin
                if (!plugin.Validate())
                {
                    _logger.LogWarning("Plugin {PluginName} failed validation", plugin.PluginName);
                    continue;
                }

                // Register the plugin
                RegisterPlugin(plugin);

                _logger.LogInformation(
                    "Loaded plugin: {PluginName} v{Version} by {Author} from {Path}",
                    plugin.PluginName,
                    plugin.Version,
                    plugin.Author,
                    assemblyPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to instantiate plugin type {Type}", pluginType.Name);
            }
        }
    }

    /// <summary>
    /// Register a plugin instance
    /// </summary>
    /// <param name="plugin">The plugin to register</param>
    public void RegisterPlugin(ILlmBackendPlugin plugin)
    {
        if (_plugins.Any(p => p.PluginId == plugin.PluginId))
        {
            _logger.LogWarning("Plugin with ID {PluginId} is already registered", plugin.PluginId);
            return;
        }

        _plugins.Add(plugin);

        // Map each supported backend type to this plugin
        foreach (var backendType in plugin.SupportedBackendTypes)
        {
            if (_pluginsByType.ContainsKey(backendType))
            {
                _logger.LogWarning(
                    "Backend type {BackendType} is already handled by plugin {ExistingPlugin}, " +
                    "new plugin {NewPlugin} will override it",
                    backendType,
                    _pluginsByType[backendType].PluginName,
                    plugin.PluginName);
            }

            _pluginsByType[backendType] = plugin;
        }
    }

    /// <summary>
    /// Get a plugin that handles a specific backend type
    /// </summary>
    /// <param name="backendType">The backend type identifier</param>
    /// <returns>The plugin that handles this backend type, or null if none found</returns>
    public ILlmBackendPlugin? GetPluginForBackendType(string backendType)
    {
        return _pluginsByType.GetValueOrDefault(backendType);
    }

    /// <summary>
    /// Check if a backend type is handled by a plugin
    /// </summary>
    /// <param name="backendType">The backend type identifier</param>
    /// <returns>True if a plugin handles this backend type</returns>
    public bool HasPluginForBackendType(string backendType)
    {
        return _pluginsByType.ContainsKey(backendType);
    }

    /// <summary>
    /// Get information about all loaded plugins
    /// </summary>
    public List<PluginInfo> GetPluginInfo()
    {
        return _plugins.Select(p => new PluginInfo
        {
            PluginId = p.PluginId,
            PluginName = p.PluginName,
            Version = p.Version,
            Author = p.Author,
            Description = p.Description,
            SupportedBackendTypes = p.SupportedBackendTypes.ToList()
        }).ToList();
    }
}

/// <summary>
/// Information about a loaded plugin
/// </summary>
public class PluginInfo
{
    public required string PluginId { get; init; }
    public required string PluginName { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }
    public required string Description { get; init; }
    public required List<string> SupportedBackendTypes { get; init; }
}
