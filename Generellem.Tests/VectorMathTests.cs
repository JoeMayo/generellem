using Generellem.Services;

namespace Generellem.Services.Tests;

public class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_IsOne()
    {
        float[] v = [1f, 2f, 3f];
        Assert.Equal(1f, VectorMath.CosineSimilarity(v, v), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_Orthogonal_IsZero()
    {
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];
        Assert.Equal(0f, VectorMath.CosineSimilarity(a, b), precision: 5);
    }

    [Fact]
    public void ToBytesAndFromBytes_RoundTrip()
    {
        float[] original = [0.1f, -2.5f, 3f];
        byte[] bytes = VectorMath.ToBytes(original);
        float[] restored = VectorMath.FromBytes(bytes).ToArray();

        Assert.Equal(original, restored);
    }
}
