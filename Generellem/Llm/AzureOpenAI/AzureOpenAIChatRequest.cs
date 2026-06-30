using Generellem.Embedding;
using Generellem.Processors;

using OpenAI.Chat;

namespace Generellem.Llm.AzureOpenAI;

public class AzureOpenAIChatRequest : IChatRequest
{
    public List<ChatMessage>? Messages { get; set; } = new();

    public ChatCompletionOptions Options { get; set; } = new();

    public QueryDetails<AzureOpenAIChatRequest, AzureOpenAIChatResponse> SummarizedUserIntent { get; set; } = new();

    public List<TextChunk> TextChunks { get; set; } = new();

    public virtual string Text 
    {
        get
        {
            if (Messages is not null && Messages.Count > 0 && Messages[0].Content is not null && Messages[0].Content.Count > 0)
                return Messages[0].Content[0].Text;
            else
                return string.Empty;
        }
        set
        {
            string? text = Messages?[0].Content.ToString();

            if (text is not null)
                if (Messages is not null && Messages.Count > 0)
                    Messages[0] = new SystemChatMessage(value);
        }
    }

    public static string GetRequestContent(ChatMessage chatMessage) =>
        chatMessage switch
        {
            AssistantChatMessage assistantMessage when assistantMessage.Content.Count > 0 =>
                assistantMessage.Content[0].Text ?? string.Empty,
            SystemChatMessage systemMessage when systemMessage.Content.Count > 0 =>
                systemMessage.Content[0].Text ?? string.Empty,
            UserChatMessage userMessage when userMessage.Content.Count > 0 =>
                userMessage.Content[0].Text ?? string.Empty,
            _ => string.Empty
        };

}
