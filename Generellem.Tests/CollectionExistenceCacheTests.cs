using Generellem.Services.Qdrant;

namespace Generellem.Services.Tests;

public class CollectionExistenceCacheTests
{
    [Fact]
    public void TryGetExists_IsFalseUntilRemembered()
    {
        CollectionExistenceCache cache = new(new ManualTime(), TimeSpan.FromSeconds(30));

        Assert.False(cache.TryGetExists());
    }

    [Fact]
    public void TryGetExists_ReturnsTrueUntilTtl()
    {
        ManualTime time = new();
        CollectionExistenceCache cache = new(time, TimeSpan.FromSeconds(30));

        cache.RememberExists();

        Assert.True(cache.TryGetExists());
        time.UtcNow = time.UtcNow.AddSeconds(29);
        Assert.True(cache.TryGetExists());
    }

    [Fact]
    public void TryGetExists_Expires()
    {
        ManualTime time = new();
        CollectionExistenceCache cache = new(time, TimeSpan.FromSeconds(30));

        cache.RememberExists();
        time.UtcNow = time.UtcNow.AddSeconds(30);

        Assert.False(cache.TryGetExists());
    }

    [Fact]
    public void Invalidate_DropsAPositiveAnswer()
    {
        CollectionExistenceCache cache = new(new ManualTime(), TimeSpan.FromMinutes(5));
        cache.RememberExists();

        cache.Invalidate();

        Assert.False(cache.TryGetExists());
    }

    [Fact]
    public void RememberExists_DoesNotCacheAMiss()
    {
        ManualTime time = new();
        CollectionExistenceCache cache = new(time, TimeSpan.FromMinutes(5));

        cache.Invalidate();
        time.UtcNow = time.UtcNow.AddMinutes(10);

        Assert.False(cache.TryGetExists());
    }

    sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
