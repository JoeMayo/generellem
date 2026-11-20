using System.Text;

using Generellem.Llm.Local;
using Generellem.Rag;

using Microsoft.Extensions.AI;

namespace Generellem.Llm.Tests;

public class ExtractiveChatClientTests
{
    readonly ExtractiveChatClient sut = new();

    [Fact]
    public async Task GetResponseAsync_IntentSummarization_ReturnsUserQuery()
    {
        ChatResponse response = await sut.GetResponseAsync(
        [
            new ChatMessage(
                ChatRole.System,
                "You're an AI assistant. Infer the user's real intent.\n\nChat History: \n\nUser's query: How do I contribute?")
        ]);

        Assert.Equal("How do I contribute?", response.Text);
    }

    [Fact]
    public async Task GetResponseAsync_WithContext_ReturnsExtractiveAnswer()
    {
        ChatResponse response = await sut.GetResponseAsync(
        [
            new ChatMessage(
                ChatRole.System,
                "You are a bot.\n\nContext: \n\n```\nContributing to Generellem requires a PR and unit tests.\n\n```\n"),
            new ChatMessage(ChatRole.User, "How do I contribute?")
        ]);

        Assert.Contains("Local demo profile", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Contributing", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetResponseAsync_WithoutContext_MentionsNoContext()
    {
        ChatResponse response = await sut.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, "You are a bot."),
            new ChatMessage(ChatRole.User, "Hello")
        ]);

        Assert.Contains("No retrieved context", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetResponseAsync_WithDelimitedContext_ReturnsExtractiveAnswer()
    {
        ChatResponse response = await sut.GetResponseAsync(
        [
            new ChatMessage(
                ChatRole.System,
                "Answer using only the untrusted context.\n" +
                UntrustedContext.Wrap("DocumentReference: fs@readme.md\nContributing requires a pull request.")),
            new ChatMessage(ChatRole.User, "How do I contribute?")
        ]);

        Assert.Contains("pull request", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fs@readme.md", response.Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_JoinsToResponseText()
    {
        ChatMessage[] messages =
        [
            new(
                ChatRole.System,
                "You are a bot.\n\nContext: \n\n```\nContributing to Generellem requires a PR and unit tests.\n\n```\n"),
            new(ChatRole.User, "How do I contribute?")
        ];

        string prompted = (await sut.GetResponseAsync(messages)).Text ?? string.Empty;
        StringBuilder streamed = new();
        await foreach (ChatResponseUpdate update in sut.GetStreamingResponseAsync(messages))
            streamed.Append(update.Text);

        Assert.Equal(prompted, streamed.ToString());
    }

    [Fact]
    public async Task GetResponseAsync_NullMessages_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetResponseAsync(null!));
    }

    [Fact]
    public void GetService_ReturnsThisForChatClient()
    {
        Assert.Same(sut, sut.GetService(typeof(IChatClient)));
        Assert.Same(sut, sut.GetService(typeof(ExtractiveChatClient)));
        Assert.Null(sut.GetService(typeof(string)));
    }
}
