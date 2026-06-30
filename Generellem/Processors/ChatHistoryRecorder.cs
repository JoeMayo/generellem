using OpenAI.Chat;

namespace Generellem.Processors;

/// <summary>
/// Records completed Q&amp;A turns for multi-turn conversations.
/// </summary>
public static class ChatHistoryRecorder
{
    /// <summary>
    /// Appends a user question and assistant answer to chat history for subsequent queries.
    /// </summary>
    /// <param name="chatHistory">Prior conversation turns.</param>
    /// <param name="userInput">The user's question.</param>
    /// <param name="assistantResponse">The assistant's answer.</param>
    public static void RecordTurn(Queue<ChatMessage> chatHistory, string userInput, string assistantResponse)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);
        ArgumentException.ThrowIfNullOrEmpty(userInput);
        ArgumentNullException.ThrowIfNull(assistantResponse);

        chatHistory.Enqueue(new UserChatMessage(userInput));
        chatHistory.Enqueue(new AssistantChatMessage(assistantResponse));
    }
}