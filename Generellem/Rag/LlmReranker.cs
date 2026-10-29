using System.Globalization;
using System.Text;

using Generellem.Embedding;
using Generellem.Llm;
using Generellem.Llm.AzureOpenAI;

using OpenAI.Chat;

namespace Generellem.Rag;

/// <summary>
/// Asks the chat model to order passages. An unparseable reply keeps the original order.
/// </summary>
public sealed class LlmReranker(ILlm llm) : IReranker
{
    public const string PromptMarker = "Rank the following passages";

    public async Task<List<TextChunk>> RerankAsync(string query, IReadOnlyList<TextChunk> candidates, CancellationToken cancellationToken)
    {
        if (candidates.Count <= 1)
            return candidates.ToList();

        StringBuilder prompt = new();
        prompt.AppendLine(PromptMarker);
        prompt.AppendLine("Reply with the passage numbers, most relevant first, separated by commas. Do not add any other text.");
        prompt.AppendLine();
        prompt.Append("Query: ");
        prompt.AppendLine(query);
        prompt.AppendLine();

        for (int i = 0; i < candidates.Count; i++)
        {
            string content = candidates[i].Content ?? string.Empty;
            if (content.Length > 500)
                content = content[..500];

            prompt.Append('[');
            prompt.Append(i.ToString(CultureInfo.InvariantCulture));
            prompt.Append("] ");
            prompt.AppendLine(content);
        }

        AzureOpenAIChatRequest request = new()
        {
            Messages = [new UserChatMessage(prompt.ToString())]
        };

        AzureOpenAIChatResponse response = await llm.PromptAsync<AzureOpenAIChatResponse>(request, cancellationToken);
        return Order(candidates, response.Text);
    }

    internal static List<TextChunk> Order(IReadOnlyList<TextChunk> candidates, string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return candidates.ToList();

        List<int> indexes = [];
        HashSet<int> seen = [];
        int current = 0;
        bool inNumber = false;

        foreach (char c in reply)
        {
            if (char.IsAsciiDigit(c))
            {
                inNumber = true;
                current = (current * 10) + (c - '0');
                continue;
            }

            if (inNumber)
            {
                Accept(current);
                current = 0;
                inNumber = false;
            }
        }

        if (inNumber)
            Accept(current);

        if (indexes.Count == 0)
            return candidates.ToList();

        for (int i = 0; i < candidates.Count; i++)
        {
            if (!seen.Contains(i))
                indexes.Add(i);
        }

        return indexes.Select(index => candidates[index]).ToList();

        void Accept(int index)
        {
            if (index < 0 || index >= candidates.Count || !seen.Add(index))
                return;

            indexes.Add(index);
        }
    }
}
