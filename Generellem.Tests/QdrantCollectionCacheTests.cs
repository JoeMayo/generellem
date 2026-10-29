using Generellem.Services.Qdrant;

using Microsoft.Extensions.Logging;

namespace Generellem.Services.Tests;

public class QdrantCollectionCacheTests
{
    [Fact]
    public async Task DoesIndexExistAsync_CachesAPositiveAnswer()
    {
        Probe sut = new();

        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));
        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));

        Assert.Equal(1, sut.ServerChecks);
    }

    [Fact]
    public async Task DoesIndexExistAsync_DoesNotReportMissingAfterExternalCreate()
    {
        Probe sut = new() { ServerExists = false };

        Assert.False(await sut.DoesIndexExistAsync(CancellationToken.None));
        sut.ServerExists = true;

        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));
        Assert.Equal(2, sut.ServerChecks);
    }

    [Fact]
    public async Task DoesIndexExistAsync_DropsStaleExistsAfterInvalidation()
    {
        Probe sut = new();

        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));
        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));
        sut.Forget();
        sut.ServerExists = false;

        Assert.False(await sut.DoesIndexExistAsync(CancellationToken.None));
        Assert.Equal(2, sut.ServerChecks);
    }

    [Fact]
    public async Task DoesIndexExistAsync_RechecksAfterTtlWhenCollectionWasDeleted()
    {
        ManualTime time = new();
        Probe sut = new(time, TimeSpan.FromSeconds(30));

        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));
        time.UtcNow = time.UtcNow.AddSeconds(31);
        sut.ServerExists = false;

        Assert.False(await sut.DoesIndexExistAsync(CancellationToken.None));
        Assert.Equal(2, sut.ServerChecks);
    }

    [Fact]
    public async Task CreateIndexAsync_RechecksServerWhenCacheSaysExists()
    {
        Probe sut = new();
        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));

        await sut.CreateIndexAsync(CancellationToken.None);

        Assert.Equal(2, sut.ServerChecks);
    }

    [Fact]
    public async Task CreateIndexAsync_DoesNotTrustStaleExistsAfterExternalDelete()
    {
        Probe sut = new();
        Assert.True(await sut.DoesIndexExistAsync(CancellationToken.None));
        sut.ServerExists = false;

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CreateIndexAsync(CancellationToken.None));

        Assert.Contains("QdrantClient", ex.Message);
        Assert.Equal(2, sut.ServerChecks);
    }

    sealed class Probe : QdrantService
    {
        public int ServerChecks;
        public bool ServerExists = true;

        public Probe()
            : this(TimeProvider.System, CollectionExistenceCache.DefaultTtl)
        {
        }

        public Probe(TimeProvider time, TimeSpan ttl)
            : base(Config(), Mock.Of<ILogger<QdrantService>>())
        {
            UseCollectionCache(time, ttl);
        }

        public void Forget() => InvalidateCollectionCache();

        protected override Task<bool> CollectionExistsOnServerAsync(CancellationToken cancellationToken)
        {
            ServerChecks++;
            return Task.FromResult(ServerExists);
        }

        protected override Task EnsureSparseVectorsAsync(CancellationToken cancelToken) =>
            Task.CompletedTask;

        static IDynamicConfiguration Config()
        {
            Mock<IDynamicConfiguration> config = new();
            config.Setup(config => config[GKeys.QdrantApiKey]).Returns("key");
            config.Setup(config => config[GKeys.QdrantEndpoint]).Returns("http://localhost:6334");
            config.Setup(config => config[GKeys.QdrantCollection]).Returns("docs");
            return config.Object;
        }
    }

    sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
