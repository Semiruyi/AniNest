namespace AniNest.Infrastructure.Thumbnails;

public enum ThumbnailDecodeStrategy
{
    Software,
    AutoHardware,
    NvidiaCuda,
    IntelQsv,
    D3D11VA
}

public interface IThumbnailDecodeStrategyService
{
    IReadOnlyList<ThumbnailDecodeStrategy> GetStrategyChain();
    ThumbnailDecodeStatusSnapshot GetStatusSnapshot();
    void RecordSuccess(ThumbnailDecodeStrategy strategy);
    void RefreshAccelerationMode();
}

public sealed record ThumbnailDecodeStatusSnapshot(
    ThumbnailAccelerationMode AccelerationMode,
    IReadOnlyList<ThumbnailDecodeStrategy> StrategyChain,
    ThumbnailDecodeStrategy? PreferredStrategy,
    bool SupportsCuda,
    bool SupportsQsv,
    bool SupportsD3D11VA);
