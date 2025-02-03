namespace Generellem.Llm;

public interface ILlm
{
    Task<TResponse> PromptAsync<TResponse>(IChatRequest request, CancellationToken cancellationToken)
        where TResponse: IChatResponse;
}
