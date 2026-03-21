using System.Buffers;
using Equalizer.Models;

namespace Equalizer.Audio;

internal sealed class AudioBufferPool
{
    private readonly ArrayPool<float> _floatPool = ArrayPool<float>.Create();
    private readonly ArrayPool<BandSnapshot> _snapshotPool = ArrayPool<BandSnapshot>.Create();

    public float[] RentFloat(int minimumLength) => _floatPool.Rent(minimumLength);
    public void ReturnFloat(float[] buffer) => _floatPool.Return(buffer);

    public BandSnapshot[] RentSnapshot(int minimumLength) => _snapshotPool.Rent(minimumLength);
    public void ReturnSnapshot(BandSnapshot[] buffer) => _snapshotPool.Return(buffer);
}