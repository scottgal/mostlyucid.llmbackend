using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Interfaces;

/// <summary>
/// Interface for managing conversation context and memory
/// </summary>
public interface IContextMemory
{
    /// <summary>
    /// Add a message to memory
    /// </summary>
    void AddMessage(string role, string content);

    /// <summary>
    /// Get all messages in memory
    /// </summary>
    List<ChatMessage> GetMessages();

    /// <summary>
    /// Clear all messages from memory
    /// </summary>
    void Clear();

    /// <summary>
    /// Estimate token count for current memory
    /// </summary>
    int EstimateTokenCount();

    /// <summary>
    /// Trim memory to fit within token limit
    /// </summary>
    void TrimToTokenLimit(int maxTokens);

    /// <summary>
    /// Save memory to external storage
    /// </summary>
    Task SaveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load memory from external storage
    /// </summary>
    Task LoadAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory implementation of context memory
/// </summary>
public class InMemoryContextMemory : IContextMemory
{
    private readonly List<ChatMessage> _messages = new();
    private readonly object _lock = new();

    public void AddMessage(string role, string content)
    {
        lock (_lock)
        {
            _messages.Add(new ChatMessage
            {
                Role = role,
                Content = content
            });
        }
    }

    public List<ChatMessage> GetMessages()
    {
        lock (_lock)
        {
            return new List<ChatMessage>(_messages);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }

    public int EstimateTokenCount()
    {
        lock (_lock)
        {
            // Rough estimation: ~4 characters per token
            return _messages.Sum(m => (m.Role.Length + m.Content.Length) / 4);
        }
    }

    public void TrimToTokenLimit(int maxTokens)
    {
        lock (_lock)
        {
            while (EstimateTokenCount() > maxTokens && _messages.Count > 1)
            {
                // Keep system messages, remove oldest user/assistant messages
                var indexToRemove = _messages.FindIndex(m => m.Role != "system");
                if (indexToRemove >= 0)
                {
                    _messages.RemoveAt(indexToRemove);
                }
                else
                {
                    break;
                }
            }
        }
    }

    public Task SaveAsync(string key, CancellationToken cancellationToken = default)
    {
        // Not implemented in basic version
        return Task.CompletedTask;
    }

    public Task LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        // Not implemented in basic version
        return Task.CompletedTask;
    }
}
