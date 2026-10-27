using System.Globalization;
using System.Net;

using Azure;

namespace Generellem.Embedding;

/// <summary>
/// Chooses a wait before retrying an embedding call that was rate limited.
/// A null delay means the caller should use its normal backoff.
/// </summary>
public static class EmbeddingRetryDelay
{
    public static TimeSpan? FromException(Exception? exception, int attempt)
    {
        switch (exception)
        {
            case RequestFailedException requestFailed:
            {
                string? retryAfter = null;
                if (requestFailed.GetRawResponse() is { } response &&
                    response.Headers.TryGetValue("Retry-After", out string? header))
                    retryAfter = header;

                return ForStatus(requestFailed.Status, retryAfter, attempt);
            }
            case HttpRequestException http when http.StatusCode is HttpStatusCode status:
                return ForStatus((int)status, null, attempt);
            default:
                return null;
        }
    }

    public static TimeSpan? ForStatus(int? statusCode, string? retryAfterHeader, int attempt)
    {
        if (statusCode != 429)
            return null;

        if (int.TryParse(retryAfterHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
            return TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 120));

        double backoffSeconds = 3 * Math.Pow(2, Math.Clamp(attempt, 0, 6));
        return TimeSpan.FromSeconds(Math.Min(60, backoffSeconds));
    }
}
