using Generellem.Llm;
using Generellem.Llm.AzureOpenAI;
using Generellem.Llm.Local;
using Generellem.Rag;

using OpenAI.Chat;

namespace Generellem.Llm.Tests;

public class LocalLlmServiceTests
{
    readonly LocalLlmService sut = new();

    [Fact]
    public async Task PromptAsync_IntentSummarization_ReturnsUserQuery()
    {
        AzureOpenAIChatRequest request = new()
        {
            Messages =
            [
                new SystemChatMessage(
                    "You're an AI assistant. Infer the user's real intent.\n\nChat History: \n\nUser's query: How do I contribute?")
            ]
        };

        AzureOpenAIChatResponse response = await sut.PromptAsync<AzureOpenAIChatResponse>(request, CancellationToken.None);

        Assert.Equal("How do I contribute?", response.Text);
    }

    [Fact]
    public async Task PromptAsync_WithContext_ReturnsExtractiveAnswer()
    {
        AzureOpenAIChatRequest request = new()
        {
            Messages =
            [
                new SystemChatMessage(
                    "You are a bot.\n\nContext: \n\n```\nContributing to Generellem requires a PR and unit tests.\n\n```\n"),
                new UserChatMessage("How do I contribute?")
            ]
        };

        AzureOpenAIChatResponse response = await sut.PromptAsync<AzureOpenAIChatResponse>(request, CancellationToken.None);

        Assert.Contains("Local demo profile", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Contributing", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PromptAsync_WithoutContext_MentionsNoContext()
    {
        AzureOpenAIChatRequest request = new()
        {
            Messages =
            [
                new SystemChatMessage("You are a bot."),
                new UserChatMessage("Hello")
            ]
        };

        AzureOpenAIChatResponse response = await sut.PromptAsync<AzureOpenAIChatResponse>(request, CancellationToken.None);

        Assert.Contains("No retrieved context", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PromptAsync_WithDelimitedContext_ReturnsExtractiveAnswer()
    {
        AzureOpenAIChatRequest request = new()
        {
            Messages =
            [
                new SystemChatMessage(
                    "Answer using only the untrusted context.\n" +
                    UntrustedContext.Wrap("DocumentReference: fs@readme.md\nContributing requires a pull request.")),
                new UserChatMessage("How do I contribute?")
            ]
        };

        AzureOpenAIChatResponse response = await sut.PromptAsync<AzureOpenAIChatResponse>(request, CancellationToken.None);

        Assert.Contains("pull request", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fs@readme.md", response.Text);
    }
}
