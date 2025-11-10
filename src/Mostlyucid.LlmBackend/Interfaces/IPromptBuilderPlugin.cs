namespace Mostlyucid.LlmBackend.Interfaces;

/// <summary>
/// Interface for prompt builder plugins that can be dynamically loaded
/// </summary>
/// <remarks>
/// Prompt builder plugins allow you to customize how prompts are constructed for different scenarios
/// (e.g., translation, chat, summarization, code generation). Plugins are discovered and loaded
/// from DLL files in the configured plugin directory.
///
/// Example scenarios:
/// - Translation: Preserve placeholders, maintain formatting
/// - Chat: Include conversation history, context variables
/// - Code Generation: Add examples, specify output format
/// - Summarization: Define length constraints, key points
///
/// Plugins can be distributed as NuGet packages and dropped into the plugins folder.
/// </remarks>
/// <example>
/// <code>
/// public class CustomTranslationPromptBuilderPlugin : IPromptBuilderPlugin
/// {
///     public string PluginId => "com.mycompany.translation.promptbuilder";
///     public string PluginName => "Advanced Translation Prompt Builder";
///
///     public IPromptBuilder CreatePromptBuilder(IContextMemory? contextMemory = null)
///     {
///         return new AdvancedTranslationPromptBuilder(contextMemory);
///     }
/// }
/// </code>
/// </example>
public interface IPromptBuilderPlugin
{
    /// <summary>
    /// Unique identifier for this plugin
    /// </summary>
    /// <remarks>
    /// Use reverse domain notation (e.g., com.company.product.feature)
    /// This must be unique across all plugins to prevent conflicts
    /// </remarks>
    string PluginId { get; }

    /// <summary>
    /// Display name for this plugin
    /// </summary>
    /// <remarks>
    /// Human-readable name shown in logs and diagnostics
    /// </remarks>
    string PluginName { get; }

    /// <summary>
    /// Plugin version (semantic versioning recommended)
    /// </summary>
    /// <example>1.0.0, 2.1.3, 1.0.0-beta</example>
    string Version { get; }

    /// <summary>
    /// Plugin author or organization
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Brief description of what this prompt builder does
    /// </summary>
    /// <remarks>
    /// Describe the use case and any special features.
    /// Example: "Specialized prompt builder for legal document translation with terminology preservation"
    /// </remarks>
    string Description { get; }

    /// <summary>
    /// Identifier for this prompt builder type (used in configuration)
    /// </summary>
    /// <remarks>
    /// This is the string used in configuration to select this prompt builder.
    /// Example: "TranslationAdvanced", "ChatWithRAG", "CodeGenerator"
    /// </remarks>
    string PromptBuilderType { get; }

    /// <summary>
    /// Create an instance of the prompt builder
    /// </summary>
    /// <param name="contextMemory">Optional context memory for conversation history.
    /// If null, the plugin may create its own or use a default implementation.</param>
    /// <returns>A fully configured IPromptBuilder instance</returns>
    /// <remarks>
    /// This method is called when a backend or service needs to use this prompt builder.
    /// The implementation should return a new instance each time if the builder is not thread-safe,
    /// or can return a shared instance if it is immutable/thread-safe.
    /// </remarks>
    IPromptBuilder CreatePromptBuilder(IContextMemory? contextMemory = null);

    /// <summary>
    /// Validate that the plugin can be loaded and initialized
    /// </summary>
    /// <returns>True if plugin is valid and ready to use, false otherwise</returns>
    /// <remarks>
    /// Perform any validation checks here such as:
    /// - Check for required dependencies
    /// - Validate configuration
    /// - Test creation of a prompt builder instance
    ///
    /// If this returns false, the plugin will not be registered.
    /// Log any validation errors using the provided logger.
    /// </remarks>
    bool Validate();
}
