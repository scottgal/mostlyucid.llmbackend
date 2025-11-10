using FluentAssertions;
using Mostlyucid.LlmBackend.Models;

namespace Mostlyucid.LlmBackend.Tests.Models;

public class LlmRequestTests
{
    [Fact]
    public void LlmRequest_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var request = new LlmRequest();

        // Assert
        request.Prompt.Should().BeEmpty();
        request.SystemMessage.Should().BeNull();
        request.Temperature.Should().BeNull();
        request.MaxTokens.Should().BeNull();
        request.TopP.Should().BeNull();
        request.FrequencyPenalty.Should().BeNull();
        request.PresencePenalty.Should().BeNull();
        request.StopSequences.Should().BeNull();
        request.Stream.Should().BeFalse();
        request.PreferredBackend.Should().BeNull();
    }

    [Fact]
    public void LlmRequest_SetProperties_ShouldWork()
    {
        // Arrange & Act
        var request = new LlmRequest
        {
            Prompt = "Test prompt",
            SystemMessage = "You are a helpful assistant",
            Temperature = 0.7,
            MaxTokens = 100,
            TopP = 0.9,
            FrequencyPenalty = 0.5,
            PresencePenalty = 0.3,
            StopSequences = new List<string> { "\n", "END" },
            Stream = true,
            PreferredBackend = "OpenAI"
        };

        // Assert
        request.Prompt.Should().Be("Test prompt");
        request.SystemMessage.Should().Be("You are a helpful assistant");
        request.Temperature.Should().Be(0.7);
        request.MaxTokens.Should().Be(100);
        request.TopP.Should().Be(0.9);
        request.FrequencyPenalty.Should().Be(0.5);
        request.PresencePenalty.Should().Be(0.3);
        request.StopSequences.Should().HaveCount(2);
        request.Stream.Should().BeTrue();
        request.PreferredBackend.Should().Be("OpenAI");
    }
}

public class LlmResponseTests
{
    [Fact]
    public void LlmResponse_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var response = new LlmResponse();

        // Assert
        response.Text.Should().BeEmpty();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().BeNull();
        response.Backend.Should().BeNull();
        response.Model.Should().BeNull();
        response.TotalTokens.Should().BeNull();
        response.PromptTokens.Should().BeNull();
        response.CompletionTokens.Should().BeNull();
        response.DurationMs.Should().Be(0);
        response.FinishReason.Should().BeNull();
        response.Exception.Should().BeNull();
        response.AlternativeResponses.Should().BeNull();
    }

    [Fact]
    public void LlmResponse_SetProperties_ShouldWork()
    {
        // Arrange & Act
        var response = new LlmResponse
        {
            Text = "Test response",
            Success = true,
            Backend = "OpenAI",
            Model = "gpt-4",
            TotalTokens = 100,
            PromptTokens = 30,
            CompletionTokens = 70,
            DurationMs = 1500,
            FinishReason = "stop"
        };

        // Assert
        response.Text.Should().Be("Test response");
        response.Success.Should().BeTrue();
        response.Backend.Should().Be("OpenAI");
        response.Model.Should().Be("gpt-4");
        response.TotalTokens.Should().Be(100);
        response.PromptTokens.Should().Be(30);
        response.CompletionTokens.Should().Be(70);
        response.DurationMs.Should().Be(1500);
        response.FinishReason.Should().Be("stop");
    }

    [Fact]
    public void LlmResponse_Content_ShouldAliasText()
    {
        // Arrange
        var response = new LlmResponse
        {
            Text = "Test content"
        };

        // Act & Assert
        response.Content.Should().Be("Test content");
        response.Content.Should().Be(response.Text);
    }

    [Fact]
    public void LlmResponse_ErrorResponse_ShouldHaveErrorDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        var response = new LlmResponse
        {
            Success = false,
            ErrorMessage = "Request failed",
            Exception = exception
        };

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Request failed");
        response.Exception.Should().Be(exception);
    }

    [Fact]
    public void LlmResponse_WithAlternatives_ShouldStoreMultipleResponses()
    {
        // Arrange
        var mainResponse = new LlmResponse
        {
            Text = "Main response",
            Success = true,
            Backend = "OpenAI"
        };

        var alt1 = new LlmResponse
        {
            Text = "Alternative 1",
            Success = true,
            Backend = "Claude"
        };

        var alt2 = new LlmResponse
        {
            Text = "Alternative 2",
            Success = true,
            Backend = "Gemini"
        };

        // Act
        mainResponse.AlternativeResponses = new List<LlmResponse> { alt1, alt2 };

        // Assert
        mainResponse.AlternativeResponses.Should().HaveCount(2);
        mainResponse.AlternativeResponses![0].Backend.Should().Be("Claude");
        mainResponse.AlternativeResponses![1].Backend.Should().Be("Gemini");
    }
}

public class ChatMessageTests
{
    [Fact]
    public void ChatMessage_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var message = new ChatMessage();

        // Assert
        message.Role.Should().BeEmpty();
        message.Content.Should().BeEmpty();
        message.Name.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_SetProperties_ShouldWork()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "user",
            Content = "Hello, world!",
            Name = "John"
        };

        // Assert
        message.Role.Should().Be("user");
        message.Content.Should().Be("Hello, world!");
        message.Name.Should().Be("John");
    }

    [Fact]
    public void ChatMessage_SystemMessage_ShouldWork()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "system",
            Content = "You are a helpful assistant"
        };

        // Assert
        message.Role.Should().Be("system");
        message.Content.Should().Be("You are a helpful assistant");
    }

    [Fact]
    public void ChatMessage_AssistantMessage_ShouldWork()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "assistant",
            Content = "How can I help you today?"
        };

        // Assert
        message.Role.Should().Be("assistant");
        message.Content.Should().Be("How can I help you today?");
    }
}

public class ChatRequestTests
{
    [Fact]
    public void ChatRequest_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var request = new ChatRequest();

        // Assert
        request.Messages.Should().NotBeNull();
        request.Messages.Should().BeEmpty();
        request.Prompt.Should().BeEmpty(); // From base class
    }

    [Fact]
    public void ChatRequest_WithMessages_ShouldWork()
    {
        // Arrange & Act
        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are helpful" },
                new ChatMessage { Role = "user", Content = "Hello" },
                new ChatMessage { Role = "assistant", Content = "Hi there!" }
            }
        };

        // Assert
        request.Messages.Should().HaveCount(3);
        request.Messages[0].Role.Should().Be("system");
        request.Messages[1].Role.Should().Be("user");
        request.Messages[2].Role.Should().Be("assistant");
    }

    [Fact]
    public void ChatRequest_InheritsFromLlmRequest_ShouldHaveAllProperties()
    {
        // Arrange & Act
        var request = new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Test" }
            },
            Temperature = 0.8,
            MaxTokens = 500,
            PreferredBackend = "Claude"
        };

        // Assert
        request.Messages.Should().HaveCount(1);
        request.Temperature.Should().Be(0.8);
        request.MaxTokens.Should().Be(500);
        request.PreferredBackend.Should().Be("Claude");
    }

    [Fact]
    public void ChatRequest_ConversationHistory_ShouldMaintainOrder()
    {
        // Arrange
        var request = new ChatRequest();

        // Act
        request.Messages.Add(new ChatMessage { Role = "user", Content = "First message" });
        request.Messages.Add(new ChatMessage { Role = "assistant", Content = "First response" });
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Second message" });

        // Assert
        request.Messages.Should().HaveCount(3);
        request.Messages[0].Content.Should().Be("First message");
        request.Messages[1].Content.Should().Be("First response");
        request.Messages[2].Content.Should().Be("Second message");
    }

    [Fact]
    public void ChatRequest_EmptyMessages_ShouldBeValid()
    {
        // Arrange & Act
        var request = new ChatRequest
        {
            Prompt = "Fallback prompt",
            Messages = new List<ChatMessage>()
        };

        // Assert
        request.Messages.Should().BeEmpty();
        request.Prompt.Should().Be("Fallback prompt");
    }
}
