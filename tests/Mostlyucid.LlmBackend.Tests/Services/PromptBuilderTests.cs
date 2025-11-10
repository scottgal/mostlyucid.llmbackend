using FluentAssertions;
using Mostlyucid.LlmBackend.Interfaces;
using Mostlyucid.LlmBackend.Models;
using Mostlyucid.LlmBackend.Services;

namespace Mostlyucid.LlmBackend.Tests.Services;

public class DefaultPromptBuilderTests
{
    [Fact]
    public void BuildPrompt_SimpleMessage_ShouldReturnFormattedPrompt()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        var userMessage = "Hello, world!";

        // Act
        var prompt = builder.BuildPrompt(userMessage);

        // Assert
        prompt.Should().Contain("User: Hello, world!");
    }

    [Fact]
    public void BuildPrompt_WithSystemMessage_ShouldIncludeSystemMessage()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        var context = new PromptContext
        {
            SystemMessage = "You are a helpful assistant"
        };

        // Act
        var prompt = builder.BuildPrompt("Test message", context);

        // Assert
        prompt.Should().Contain("System: You are a helpful assistant");
        prompt.Should().Contain("User: Test message");
    }

    [Fact]
    public void BuildPrompt_WithExamples_ShouldIncludeFewShotExamples()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        var context = new PromptContext
        {
            Examples = new List<PromptExample>
            {
                new PromptExample { Input = "Hi", Output = "Hello!" },
                new PromptExample { Input = "How are you?", Output = "I'm doing well!" }
            }
        };

        // Act
        var prompt = builder.BuildPrompt("Test message", context);

        // Assert
        prompt.Should().Contain("Examples:");
        prompt.Should().Contain("Input: Hi");
        prompt.Should().Contain("Output: Hello!");
        prompt.Should().Contain("Input: How are you?");
        prompt.Should().Contain("Output: I'm doing well!");
    }

    [Fact]
    public void BuildPrompt_WithContextVariables_ShouldIncludeContext()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        var context = new PromptContext
        {
            ContextVariables = new Dictionary<string, string>
            {
                ["UserName"] = "John",
                ["Location"] = "New York"
            }
        };

        // Act
        var prompt = builder.BuildPrompt("Test message", context);

        // Assert
        prompt.Should().Contain("Context:");
        prompt.Should().Contain("UserName: John");
        prompt.Should().Contain("Location: New York");
    }

    [Fact]
    public void BuildPrompt_WithTemplateVariables_ShouldReplaceVariables()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        var context = new PromptContext
        {
            TemplateVariables = new Dictionary<string, string>
            {
                ["name"] = "Alice",
                ["age"] = "30"
            }
        };

        // Act
        var prompt = builder.BuildPrompt("Hello {{name}}, you are {{age}} years old", context);

        // Assert
        prompt.Should().Contain("Hello Alice, you are 30 years old");
        prompt.Should().NotContain("{{name}}");
        prompt.Should().NotContain("{{age}}");
    }

    [Fact]
    public void BuildPrompt_WithHistory_ShouldIncludeConversationHistory()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        builder.AddToMemory("user", "First message");
        builder.AddToMemory("assistant", "First response");
        builder.AddToMemory("user", "Second message");

        var context = new PromptContext
        {
            IncludeHistory = true,
            MaxMemoryMessages = 10
        };

        // Act
        var prompt = builder.BuildPrompt("Current message", context);

        // Assert
        prompt.Should().Contain("user: First message");
        prompt.Should().Contain("assistant: First response");
        prompt.Should().Contain("user: Second message");
        prompt.Should().Contain("User: Current message");
    }

    [Fact]
    public void BuildPrompt_WithHistoryLimit_ShouldLimitMessages()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        builder.AddToMemory("user", "Message 1");
        builder.AddToMemory("assistant", "Response 1");
        builder.AddToMemory("user", "Message 2");
        builder.AddToMemory("assistant", "Response 2");
        builder.AddToMemory("user", "Message 3");

        var context = new PromptContext
        {
            IncludeHistory = true,
            MaxMemoryMessages = 2 // Only last 2 messages
        };

        // Act
        var prompt = builder.BuildPrompt("Current message", context);

        // Assert
        prompt.Should().NotContain("Message 1");
        prompt.Should().NotContain("Response 1");
        prompt.Should().NotContain("Message 2");
        prompt.Should().Contain("Response 2");
        prompt.Should().Contain("Message 3");
    }

    [Fact]
    public void BuildChatRequest_SimpleMessage_ShouldCreateChatRequest()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();

        // Act
        var request = builder.BuildChatRequest("Hello");

        // Assert
        request.Should().NotBeNull();
        request.Messages.Should().HaveCount(1);
        request.Messages[0].Role.Should().Be("user");
        request.Messages[0].Content.Should().Be("Hello");
    }

    [Fact]
    public void BuildChatRequest_WithSystemMessage_ShouldAddSystemMessage()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        var context = new PromptContext
        {
            SystemMessage = "You are a helpful assistant"
        };

        // Act
        var request = builder.BuildChatRequest("Hello", context);

        // Assert
        request.Messages.Should().HaveCount(2);
        request.Messages[0].Role.Should().Be("system");
        request.Messages[0].Content.Should().Be("You are a helpful assistant");
        request.Messages[1].Role.Should().Be("user");
        request.Messages[1].Content.Should().Be("Hello");
    }

    [Fact]
    public void BuildChatRequest_WithExamples_ShouldAddExamplesAsMessages()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        var context = new PromptContext
        {
            Examples = new List<PromptExample>
            {
                new PromptExample { Input = "Hi", Output = "Hello!" }
            }
        };

        // Act
        var request = builder.BuildChatRequest("Test", context);

        // Assert
        request.Messages.Should().HaveCount(3); // example user + example assistant + current user
        request.Messages[0].Role.Should().Be("user");
        request.Messages[0].Content.Should().Be("Hi");
        request.Messages[1].Role.Should().Be("assistant");
        request.Messages[1].Content.Should().Be("Hello!");
        request.Messages[2].Role.Should().Be("user");
        request.Messages[2].Content.Should().Be("Test");
    }

    [Fact]
    public void BuildChatRequest_WithHistory_ShouldIncludeHistory()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        builder.AddToMemory("user", "Previous message");
        builder.AddToMemory("assistant", "Previous response");

        var context = new PromptContext
        {
            IncludeHistory = true,
            MaxMemoryMessages = 10
        };

        // Act
        var request = builder.BuildChatRequest("Current message", context);

        // Assert
        request.Messages.Should().HaveCount(3);
        request.Messages[0].Role.Should().Be("user");
        request.Messages[0].Content.Should().Be("Previous message");
        request.Messages[1].Role.Should().Be("assistant");
        request.Messages[1].Content.Should().Be("Previous response");
        request.Messages[2].Role.Should().Be("user");
        request.Messages[2].Content.Should().Be("Current message");
    }

    [Fact]
    public void AddToMemory_ShouldStoreMessages()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();

        // Act
        builder.AddToMemory("user", "Test message");
        var memory = builder.GetMemory();

        // Assert
        memory.Should().HaveCount(1);
        memory[0].Role.Should().Be("user");
        memory[0].Content.Should().Be("Test message");
    }

    [Fact]
    public void ClearMemory_ShouldRemoveAllMessages()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        builder.AddToMemory("user", "Message 1");
        builder.AddToMemory("assistant", "Response 1");

        // Act
        builder.ClearMemory();
        var memory = builder.GetMemory();

        // Assert
        memory.Should().BeEmpty();
    }

    [Fact]
    public void GetMemory_ShouldReturnAllStoredMessages()
    {
        // Arrange
        var builder = new DefaultPromptBuilder();
        builder.AddToMemory("user", "Message 1");
        builder.AddToMemory("assistant", "Response 1");
        builder.AddToMemory("user", "Message 2");

        // Act
        var memory = builder.GetMemory();

        // Assert
        memory.Should().HaveCount(3);
        memory[0].Content.Should().Be("Message 1");
        memory[1].Content.Should().Be("Response 1");
        memory[2].Content.Should().Be("Message 2");
    }
}

public class TranslationPromptBuilderTests
{
    [Fact]
    public void BuildPrompt_DefaultLanguages_ShouldCreateTranslationPrompt()
    {
        // Arrange
        var builder = new TranslationPromptBuilder();
        var text = "Hello, world!";

        // Act
        var prompt = builder.BuildPrompt(text);

        // Assert
        prompt.Should().Contain("Translate the following text from auto to English");
        prompt.Should().Contain("Hello, world!");
        prompt.Should().Contain("Preserve all formatting");
        prompt.Should().Contain("Provide only the translation");
    }

    [Fact]
    public void BuildPrompt_WithCustomLanguages_ShouldUseSpecifiedLanguages()
    {
        // Arrange
        var builder = new TranslationPromptBuilder();
        var context = new PromptContext
        {
            ContextVariables = new Dictionary<string, string>
            {
                ["SourceLanguage"] = "French",
                ["TargetLanguage"] = "Spanish"
            }
        };

        // Act
        var prompt = builder.BuildPrompt("Bonjour", context);

        // Assert
        prompt.Should().Contain("Translate the following text from French to Spanish");
        prompt.Should().Contain("Bonjour");
    }

    [Fact]
    public void BuildPrompt_WithContext_ShouldIncludeContextInformation()
    {
        // Arrange
        var builder = new TranslationPromptBuilder();
        var context = new PromptContext
        {
            ContextVariables = new Dictionary<string, string>
            {
                ["Context"] = "Technical documentation"
            }
        };

        // Act
        var prompt = builder.BuildPrompt("API endpoint", context);

        // Assert
        prompt.Should().Contain("Context: Technical documentation");
        prompt.Should().Contain("API endpoint");
    }

    [Fact]
    public void BuildPrompt_ShouldPreservePlaceholders()
    {
        // Arrange
        var builder = new TranslationPromptBuilder();
        var text = "Hello {0}, welcome to {{app}}!";

        // Act
        var prompt = builder.BuildPrompt(text);

        // Assert
        prompt.Should().Contain("Preserve all formatting, placeholders");
        prompt.Should().Contain("Hello {0}, welcome to {{app}}!");
    }

    [Fact]
    public void BuildChatRequest_ShouldCreateChatRequest()
    {
        // Arrange
        var builder = new TranslationPromptBuilder();
        var text = "Hello, world!";

        // Act
        var request = builder.BuildChatRequest(text);

        // Assert
        request.Should().NotBeNull();
        request.Messages.Should().HaveCount(1);
        request.Messages[0].Role.Should().Be("user");
        request.Messages[0].Content.Should().Contain("Translate");
        request.Messages[0].Content.Should().Contain("Hello, world!");
    }

    [Fact]
    public void AddToMemory_ShouldStoreMessages()
    {
        // Arrange
        var builder = new TranslationPromptBuilder();

        // Act
        builder.AddToMemory("user", "Source text");
        builder.AddToMemory("assistant", "Translated text");
        var memory = builder.GetMemory();

        // Assert
        memory.Should().HaveCount(2);
        memory[0].Content.Should().Be("Source text");
        memory[1].Content.Should().Be("Translated text");
    }

    [Fact]
    public void ClearMemory_ShouldRemoveAllMessages()
    {
        // Arrange
        var builder = new TranslationPromptBuilder();
        builder.AddToMemory("user", "Test");

        // Act
        builder.ClearMemory();
        var memory = builder.GetMemory();

        // Assert
        memory.Should().BeEmpty();
    }
}
