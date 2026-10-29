namespace Generellem.Llm;

public interface ILlm
{
    Task<TResponse> PromptAsync<TResponse>(IChatRequest request, CancellationToken cancellationToken)
        where TResponse: IChatResponse;

    /// <summary>
    /// Streams the assistant reply as text fragments.
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(IChatRequest request, CancellationToken cancellationToken);
}
