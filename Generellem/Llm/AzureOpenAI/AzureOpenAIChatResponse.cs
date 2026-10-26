using OpenAI.Chat;

namespace Generellem.Llm;

public class AzureOpenAIChatResponse : IChatResponse
{
    string? textOverride;

    /// <summary>
    /// This constructor only supports unit testing - don't use for anything else.
    /// </summary>
    public AzureOpenAIChatResponse()
    {
    }

    /// <summary>
    /// Creates a response with explicit text (local/Ollama LLMs that do not use OpenAI ChatCompletion).
    /// </summary>
    public AzureOpenAIChatResponse(string text)
    {
        textOverride = text;
    }

    public AzureOpenAIChatResponse(ChatCompletion chatCompletionsResponse)
    {
        ChatCompletionsResponse = chatCompletionsResponse;
    }

    public virtual ChatCompletion? ChatCompletionsResponse { get; init; }

    public virtual string Text
    {
        get
        {
            if (textOverride is not null)
                return textOverride;

            if (ChatCompletionsResponse is not null && ChatCompletionsResponse.Content is not null && ChatCompletionsResponse.Content.Count > 0)
                return ChatCompletionsResponse.Content[0].Text;
            else
                return string.Empty;
        }
        set => textOverride = value;
    }
}
