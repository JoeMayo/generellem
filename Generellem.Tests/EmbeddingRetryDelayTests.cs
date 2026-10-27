using System.Net;

using Azure;

using Generellem.Embedding;

namespace Generellem.Embedding.Tests;

public class EmbeddingRetryDelayTests
{
    [Fact]
    public void ForStatus_UsesRetryAfterSeconds()
    {
        TimeSpan? delay = EmbeddingRetryDelay.ForStatus(429, "2", attempt: 0);

        Assert.Equal(TimeSpan.FromSeconds(2), delay);
    }

    [Fact]
    public void ForStatus_UsesBackoffWhenRetryAfterIsMissing()
    {
        TimeSpan? delay = EmbeddingRetryDelay.ForStatus(429, null, attempt: 1);

        Assert.Equal(TimeSpan.FromSeconds(6), delay);
    }

    [Fact]
    public void ForStatus_IgnoresOtherStatusCodes()
    {
        Assert.Null(EmbeddingRetryDelay.ForStatus(500, "2", attempt: 0));
        Assert.Null(EmbeddingRetryDelay.ForStatus(401, null, attempt: 0));
    }

    [Fact]
    public void FromException_RecognizesAzureRateLimit()
    {
        RequestFailedException exception = new(429, "slow down");

        TimeSpan? delay = EmbeddingRetryDelay.FromException(exception, attempt: 0);

        Assert.Equal(TimeSpan.FromSeconds(3), delay);
    }

    [Fact]
    public void FromException_LeavesNormalBackoffForOtherAzureErrors()
    {
        RequestFailedException exception = new("Unauthorized");

        Assert.Null(EmbeddingRetryDelay.FromException(exception, attempt: 0));
    }

    [Fact]
    public void FromException_RecognizesHttp429()
    {
        HttpRequestException exception = new("slow down", null, HttpStatusCode.TooManyRequests);

        TimeSpan? delay = EmbeddingRetryDelay.FromException(exception, attempt: 0);

        Assert.Equal(TimeSpan.FromSeconds(3), delay);
    }
}
