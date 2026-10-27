namespace Generellem.Services.Qdrant;

/// <summary>
/// Remembers that a Qdrant collection exists.
/// A missing collection is never cached, so a collection created outside this process
/// is visible on the next check. A cached "exists" answer expires and is dropped on NotFound.
/// </summary>
public sealed class CollectionExistenceCache
{
    /// <summary>
    /// How long a positive existence answer is trusted before Qdrant is asked again.
    /// </summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

    readonly object gate = new();
    readonly TimeProvider time;
    readonly TimeSpan ttl;
    bool exists;
    DateTimeOffset expiresAt = DateTimeOffset.MinValue;

    public CollectionExistenceCache(TimeProvider? time = null, TimeSpan? ttl = null)
    {
        this.time = time ?? TimeProvider.System;
        this.ttl = ttl ?? DefaultTtl;
    }

    public bool TryGetExists()
    {
        lock (gate)
            return exists && time.GetUtcNow() < expiresAt;
    }

    public void RememberExists()
    {
        lock (gate)
        {
            exists = true;
            expiresAt = time.GetUtcNow().Add(ttl);
        }
    }

    public void Invalidate()
    {
        lock (gate)
        {
            exists = false;
            expiresAt = DateTimeOffset.MinValue;
        }
    }
}
