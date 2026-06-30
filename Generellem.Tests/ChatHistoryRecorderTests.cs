using Generellem.Processors;

using OpenAI.Chat;

namespace Generellem.Processors.Tests;

public class ChatHistoryRecorderTests
{
    [Fact]
    public void RecordTurn_EnqueuesUserAndAssistantMessages()
    {
        Queue<ChatMessage> chatHistory = new();

        ChatHistoryRecorder.RecordTurn(chatHistory, "What is Generellem?", "A RAG framework.");

        Assert.Equal(2, chatHistory.Count);
        Assert.IsType<UserChatMessage>(chatHistory.Dequeue());
        Assert.IsType<AssistantChatMessage>(chatHistory.Dequeue());
    }

    [Fact]
    public void RecordTurn_PreservesMessageContent()
    {
        Queue<ChatMessage> chatHistory = new();
        const string question = "How do I contribute?";
        const string answer = "Open a pull request.";

        ChatHistoryRecorder.RecordTurn(chatHistory, question, answer);

        UserChatMessage userMessage = Assert.IsType<UserChatMessage>(chatHistory.Dequeue());
        AssistantChatMessage assistantMessage = Assert.IsType<AssistantChatMessage>(chatHistory.Dequeue());

        Assert.Equal(question, userMessage.Content[0].Text);
        Assert.Equal(answer, assistantMessage.Content[0].Text);
    }

    [Fact]
    public void RecordTurn_AccumulatesMultipleTurns()
    {
        Queue<ChatMessage> chatHistory = new();

        ChatHistoryRecorder.RecordTurn(chatHistory, "First question", "First answer");
        ChatHistoryRecorder.RecordTurn(chatHistory, "Second question", "Second answer");

        Assert.Equal(4, chatHistory.Count);
    }

    [Fact]
    public void RecordTurn_WithNullChatHistory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ChatHistoryRecorder.RecordTurn(null!, "question", "answer"));
    }

    [Fact]
    public void RecordTurn_WithEmptyUserInput_ThrowsArgumentException()
    {
        Queue<ChatMessage> chatHistory = new();

        Assert.Throws<ArgumentException>(() =>
            ChatHistoryRecorder.RecordTurn(chatHistory, string.Empty, "answer"));
    }

    [Fact]
    public void RecordTurn_WithNullAssistantResponse_ThrowsArgumentNullException()
    {
        Queue<ChatMessage> chatHistory = new();

        Assert.Throws<ArgumentNullException>(() =>
            ChatHistoryRecorder.RecordTurn(chatHistory, "question", null!));
    }
}