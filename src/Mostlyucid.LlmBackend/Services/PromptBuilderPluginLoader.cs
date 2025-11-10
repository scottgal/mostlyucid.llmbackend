using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Mostlyucid.LlmBackend.Interfaces;

namespace Mostlyucid.LlmBackend.Services;

/// <summary>
/// Loads prompt builder plugins from assemblies
/// </summary>
/// <remarks>
/// <para>
/// This class is responsible for discovering and loading prompt builder plugins
/// from DLL files. Plugins allow you to extend the library with custom prompt
/// building logic for different scenarios (translation, chat, summarization, etc.)
/// without modifying the core library.
/// </para>
///
/// <para><strong>Plugin Discovery:</strong></para>
/// Plugins are discovered by:
/// 1. Scanning a configured directory for DLL files
/// 2. Loading each assembly and looking for types implementing IPromptBuilderPlugin
/// 3. Instantiating and validating each plugin
/// 4. Registering valid plugins for use
///
/// <para><strong>Example Plugin Structure:</strong></para>
/// <code>
/// MyProject.CustomPromptBuilders/
/// ├── MyCustomPlugin.cs          (implements IPromptBuilderPlugin)
/// ├── CustomPromptBuilder.cs     (implements IPromptBuilder)
/// └── MyProject.CustomPromptBuilders.csproj
/// </code>
///
/// <para><strong>Deployment:</strong></para>
/// Compiled plugins (*.dll) can be deployed by:
/// 1. Copying to the configured plugins directory (default: "plugins/")
/// 2. Specifying exact paths in configuration
/// 3. Distributing as NuGet packages
///
/// <para><strong>Thread Safety:</strong></para>
/// This class is thread-safe for reading after initialization.
/// Do not call load methods concurrently.
/// </remarks>
public class PromptBuilderPluginLoader
{
    private readonly ILogger<PromptBuilderPluginLoader> _logger;
    private readonly List<IPromptBuilderPlugin> _plugins = new();
    private readonly Dictionary<string, IPromptBuilderPlugin> _pluginsByType = new();

    /// <summary>
    /// Initializes a new instance of the PromptBuilderPluginLoader
    /// </summary>
    /// <param name="logger">Logger for recording plugin loading activity and errors</param>
    /// <remarks>
    /// The logger will receive messages at different levels:
    /// - Debug: Detailed plugin loading progress
    /// - Information: Successfully loaded plugins
    /// - Warning: Non-critical issues (missing directories, validation failures)
    /// - Error: Critical failures (cannot load assembly, instantiation errors)
    /// </remarks>
    public PromptBuilderPluginLoader(ILogger<PromptBuilderPluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all successfully loaded plugins
    /// </summary>
    /// <value>Read-only list of all registered plugins</value>
    /// <remarks>
    /// Returns plugins in the order they were loaded.
    /// This list does not include plugins that failed validation.
    /// </remarks>
    public IReadOnlyList<IPromptBuilderPlugin> Plugins => _plugins.AsReadOnly();

    /// <summary>
    /// Load prompt builder plugins from a directory
    /// </summary>
    /// <param name="pluginDirectory">Directory path containing plugin DLL files</param>
    /// <remarks>
    /// <para>
    /// Recursively scans the directory for *.dll files and attempts to load
    /// each as a potential plugin assembly.
    /// </para>
    ///
    /// <para><strong>Logging:</strong></para>
    /// - Information: Directory being scanned, plugins found and loaded
    /// - Warning: Directory doesn't exist, no plugins found
    /// - Error: Failed to load specific DLL files
    /// - Debug: Details of each file being examined
    ///
    /// <para><strong>Error Handling:</strong></para>
    /// Errors loading individual plugins do not stop the process.
    /// Other plugins in the directory will still be loaded.
    /// All errors are logged for troubleshooting.
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// loader.LoadPluginsFromDirectory("./plugins");
    /// // Loads from: ./plugins/*.dll and ./plugins/**/*.dll
    /// </code>
    /// </remarks>
    public void LoadPluginsFromDirectory(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogWarning(
                "Prompt builder plugin directory {Directory} does not exist",
                pluginDirectory);
            return;
        }

        _logger.LogInformation(
            "Loading prompt builder plugins from {Directory}",
            pluginDirectory);

        var dllFiles = Directory.GetFiles(
            pluginDirectory,
            "*.dll",
            SearchOption.AllDirectories);

        _logger.LogDebug(
            "Found {Count} DLL files to examine for prompt builder plugins",
            dllFiles.Length);

        var loadedCount = 0;
        foreach (var dllPath in dllFiles)
        {
            try
            {
                _logger.LogDebug("Examining {Path} for prompt builder plugins", dllPath);
                var loaded = LoadPluginFromAssembly(dllPath);
                if (loaded > 0)
                {
                    loadedCount += loaded;
                    _logger.LogDebug(
                        "Loaded {Count} prompt builder plugin(s) from {Path}",
                        loaded,
                        dllPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load prompt builder plugin from {Path}",
                    dllPath);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} prompt builder plugin(s) from {Directory}",
            loadedCount,
            pluginDirectory);
    }

    /// <summary>
    /// Load prompt builder plugins from a specific assembly file
    /// </summary>
    /// <param name="assemblyPath">Full path to the plugin assembly DLL</param>
    /// <returns>Number of plugins loaded from this assembly</returns>
    /// <remarks>
    /// <para>
    /// Loads a specific assembly and searches for types implementing IPromptBuilderPlugin.
    /// Multiple plugin types can exist in a single assembly.
    /// </para>
    ///
    /// <para><strong>Validation:</strong></para>
    /// Each plugin type must:
    /// 1. Implement IPromptBuilderPlugin interface
    /// 2. Have a parameterless constructor
    /// 3. Pass the Validate() check
    ///
    /// <para><strong>Logging:</strong></para>
    /// - Debug: Assembly loading, types being examined
    /// - Information: Successful plugin registration with details
    /// - Warning: Validation failures, missing constructors
    /// - Error: Assembly load failures, instantiation errors
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var count = loader.LoadPluginFromAssembly(
    ///     "/path/to/MyCompany.PromptBuilders.dll");
    /// Console.WriteLine($"Loaded {count} plugins");
    /// </code>
    /// </remarks>
    /// <exception cref="FileNotFoundException">If assembly file doesn't exist</exception>
    /// <exception cref="BadImageFormatException">If file is not a valid .NET assembly</exception>
    public int LoadPluginFromAssembly(string assemblyPath)
    {
        _logger.LogDebug("Loading assembly from {Path}", assemblyPath);

        // Load the assembly
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

        _logger.LogDebug(
            "Searching assembly {AssemblyName} for prompt builder plugin types",
            assembly.FullName);

        // Find types that implement IPromptBuilderPlugin
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPromptBuilderPlugin).IsAssignableFrom(t) &&
                       !t.IsInterface &&
                       !t.IsAbstract);

        var loadedCount = 0;
        foreach (var pluginType in pluginTypes)
        {
            try
            {
                _logger.LogDebug(
                    "Attempting to instantiate prompt builder plugin type {Type}",
                    pluginType.FullName);

                // Create an instance of the plugin
                var plugin = Activator.CreateInstance(pluginType) as IPromptBuilderPlugin;

                if (plugin == null)
                {
                    _logger.LogWarning(
                        "Failed to create instance of prompt builder plugin type {Type}",
                        pluginType.Name);
                    continue;
                }

                _logger.LogDebug(
                    "Validating prompt builder plugin {PluginName} ({PluginId})",
                    plugin.PluginName,
                    plugin.PluginId);

                // Validate the plugin
                if (!plugin.Validate())
                {
                    _logger.LogWarning(
                        "Prompt builder plugin {PluginName} failed validation",
                        plugin.PluginName);
                    continue;
                }

                // Register the plugin
                RegisterPlugin(plugin);
                loadedCount++;

                _logger.LogInformation(
                    "Loaded prompt builder plugin: {PluginName} v{Version} by {Author} " +
                    "(Type: {Type}) from {Path}",
                    plugin.PluginName,
                    plugin.Version,
                    plugin.Author,
                    plugin.PromptBuilderType,
                    assemblyPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to instantiate prompt builder plugin type {Type}",
                    pluginType.Name);
            }
        }

        return loadedCount;
    }

    /// <summary>
    /// Register a prompt builder plugin instance
    /// </summary>
    /// <param name="plugin">The plugin instance to register</param>
    /// <remarks>
    /// <para>
    /// Registers the plugin for use by mapping its PromptBuilderType to the plugin instance.
    /// If a plugin for the same type already exists, it will be overridden with a warning.
    /// </para>
    ///
    /// <para><strong>Duplicate Handling:</strong></para>
    /// - Plugin ID duplicates: Logged as warning, plugin not registered
    /// - Prompt builder type duplicates: Logged as warning, new plugin overrides old one
    ///
    /// <para><strong>Logging:</strong></para>
    /// - Debug: Plugin registration details
    /// - Warning: Duplicate IDs or types being overridden
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var plugin = new MyCustomPromptBuilderPlugin();
    /// loader.RegisterPlugin(plugin);
    /// </code>
    /// </remarks>
    public void RegisterPlugin(IPromptBuilderPlugin plugin)
    {
        _logger.LogDebug(
            "Registering prompt builder plugin {PluginId} for type {Type}",
            plugin.PluginId,
            plugin.PromptBuilderType);

        if (_plugins.Any(p => p.PluginId == plugin.PluginId))
        {
            _logger.LogWarning(
                "Prompt builder plugin with ID {PluginId} is already registered, skipping",
                plugin.PluginId);
            return;
        }

        _plugins.Add(plugin);

        // Map the prompt builder type to this plugin
        var builderType = plugin.PromptBuilderType;
        if (_pluginsByType.ContainsKey(builderType))
        {
            _logger.LogWarning(
                "Prompt builder type {Type} is already handled by plugin {ExistingPlugin}, " +
                "new plugin {NewPlugin} will override it",
                builderType,
                _pluginsByType[builderType].PluginName,
                plugin.PluginName);
        }

        _pluginsByType[builderType] = plugin;

        _logger.LogDebug(
            "Successfully registered prompt builder plugin {PluginName} for type {Type}",
            plugin.PluginName,
            builderType);
    }

    /// <summary>
    /// Get a plugin that provides a specific prompt builder type
    /// </summary>
    /// <param name="promptBuilderType">The prompt builder type identifier</param>
    /// <returns>The plugin that provides this prompt builder type, or null if none found</returns>
    /// <remarks>
    /// <para>
    /// Use this method to get the plugin responsible for creating a specific type
    /// of prompt builder (e.g., "TranslationPromptBuilder", "ChatPromptBuilder").
    /// </para>
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var plugin = loader.GetPluginForPromptBuilderType("CustomTranslation");
    /// if (plugin != null)
    /// {
    ///     var builder = plugin.CreatePromptBuilder();
    ///     // Use the builder...
    /// }
    /// </code>
    /// </remarks>
    public IPromptBuilderPlugin? GetPluginForPromptBuilderType(string promptBuilderType)
    {
        return _pluginsByType.GetValueOrDefault(promptBuilderType);
    }

    /// <summary>
    /// Check if a prompt builder type is handled by a plugin
    /// </summary>
    /// <param name="promptBuilderType">The prompt builder type identifier</param>
    /// <returns>True if a plugin handles this prompt builder type</returns>
    /// <remarks>
    /// Use this to check if a configured prompt builder type is available
    /// before attempting to use it.
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// if (loader.HasPluginForPromptBuilderType("CustomTranslation"))
    /// {
    ///     // Use custom translation prompt builder
    /// }
    /// else
    /// {
    ///     // Fall back to default
    /// }
    /// </code>
    /// </remarks>
    public bool HasPluginForPromptBuilderType(string promptBuilderType)
    {
        return _pluginsByType.ContainsKey(promptBuilderType);
    }

    /// <summary>
    /// Get information about all loaded plugins
    /// </summary>
    /// <returns>List of plugin information objects</returns>
    /// <remarks>
    /// Returns summary information about each loaded plugin.
    /// Useful for diagnostics, logging, and admin interfaces.
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var plugins = loader.GetPluginInfo();
    /// foreach (var plugin in plugins)
    /// {
    ///     Console.WriteLine($"{plugin.PluginName} v{plugin.Version}");
    ///     Console.WriteLine($"  Type: {plugin.PromptBuilderType}");
    ///     Console.WriteLine($"  By: {plugin.Author}");
    /// }
    /// </code>
    /// </remarks>
    public List<PromptBuilderPluginInfo> GetPluginInfo()
    {
        return _plugins.Select(p => new PromptBuilderPluginInfo
        {
            PluginId = p.PluginId,
            PluginName = p.PluginName,
            Version = p.Version,
            Author = p.Author,
            Description = p.Description,
            PromptBuilderType = p.PromptBuilderType
        }).ToList();
    }
}

/// <summary>
/// Information about a loaded prompt builder plugin
/// </summary>
/// <remarks>
/// Contains metadata about a plugin without holding a reference to the plugin itself.
/// Safe to serialize and pass across boundaries.
/// </remarks>
public class PromptBuilderPluginInfo
{
    /// <summary>
    /// Unique plugin identifier
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Human-readable plugin name
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    /// Plugin version string
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Plugin author or organization
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// Plugin description
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The prompt builder type this plugin provides
    /// </summary>
    public required string PromptBuilderType { get; init; }
}
