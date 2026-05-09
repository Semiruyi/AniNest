using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;

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

internal sealed record ThumbnailHardwareProbeResult(
    bool SupportsCuda,
    bool SupportsQsv,
    bool SupportsD3D11VA);

public sealed record ThumbnailDecodeStatusSnapshot(
    ThumbnailAccelerationMode AccelerationMode,
    IReadOnlyList<ThumbnailDecodeStrategy> StrategyChain,
    ThumbnailDecodeStrategy? PreferredStrategy,
    bool SupportsCuda,
    bool SupportsQsv,
    bool SupportsD3D11VA);

public sealed class ThumbnailDecodeStrategyService : IThumbnailDecodeStrategyService
{
    private static readonly Logger Log = AppLog.For<ThumbnailDecodeStrategyService>();

    private readonly ISettingsService _settings;
    private readonly Func<string> _machineIdProvider;
    private readonly Func<ThumbnailHardwareProbeResult> _probeFactory;
    private readonly object _sync = new();

    private bool _probeInitialized;
    private ThumbnailHardwareProbeResult _probe = new(false, false, false);
    private ThumbnailAccelerationMode _accelerationMode;

    public ThumbnailDecodeStrategyService(ISettingsService settings)
        : this(settings, ComputeMachineId, ProbeHardwareCapabilities)
    {
    }

    internal ThumbnailDecodeStrategyService(
        ISettingsService settings,
        Func<string> machineIdProvider,
        Func<ThumbnailHardwareProbeResult> probeFactory)
    {
        _settings = settings;
        _machineIdProvider = machineIdProvider;
        _probeFactory = probeFactory;
        _accelerationMode = _settings.GetThumbnailAccelerationMode();
    }

    public IReadOnlyList<ThumbnailDecodeStrategy> GetStrategyChain()
    {
        lock (_sync)
        {
            var snapshot = BuildStatusSnapshot();

            Log.Info(
                $"Thumbnail decode strategy chain: machine={_settings.Load().ThumbnailDecoderMachineId}, " +
                $"preferred={_settings.Load().ThumbnailPreferredDecoder}, acceleration={_accelerationMode}, chain={string.Join(" -> ", snapshot.StrategyChain)}");
            return snapshot.StrategyChain;
        }
    }

    public ThumbnailDecodeStatusSnapshot GetStatusSnapshot()
    {
        lock (_sync)
        {
            return BuildStatusSnapshot();
        }
    }

    public void RecordSuccess(ThumbnailDecodeStrategy strategy)
    {
        lock (_sync)
        {
            EnsureMachineBinding();
            var settings = _settings.Load();
            string code = strategy.ToString();
            if (string.Equals(settings.ThumbnailPreferredDecoder, code, StringComparison.Ordinal))
                return;

            settings.ThumbnailPreferredDecoder = code;
            _settings.Save();
            Log.Info($"Thumbnail decode preferred strategy updated: {code}");
        }
    }

    public void RefreshAccelerationMode()
    {
        lock (_sync)
        {
            ThumbnailAccelerationMode mode = _settings.GetThumbnailAccelerationMode();
            if (_accelerationMode == mode)
                return;

            _accelerationMode = mode;
            Log.Info($"Thumbnail acceleration mode changed: mode={mode}");
        }
    }

    internal static string ComputeMachineId()
    {
        string raw = string.Join("|",
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture,
            RuntimeInformation.ProcessArchitecture);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    internal static ThumbnailHardwareProbeResult ProbeHardwareCapabilities()
    {
        if (!File.Exists(AppPaths.FfmpegPath))
            return new ThumbnailHardwareProbeResult(false, false, false);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppPaths.FfmpegPath,
                Arguments = "-hide_banner -hwaccels",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return new ThumbnailHardwareProbeResult(false, false, false);

            string output = process.StandardOutput.ReadToEnd();
            output += process.StandardError.ReadToEnd();
            process.WaitForExit(3000);

            bool supportsCuda = output.Contains("cuda", StringComparison.OrdinalIgnoreCase);
            bool supportsQsv = output.Contains("qsv", StringComparison.OrdinalIgnoreCase);
            bool supportsD3d11va = output.Contains("d3d11va", StringComparison.OrdinalIgnoreCase);

            return new ThumbnailHardwareProbeResult(supportsCuda, supportsQsv, supportsD3d11va);
        }
        catch (Exception ex)
        {
            Log.Warning($"Thumbnail hardware probe failed: {ex.Message}");
            return new ThumbnailHardwareProbeResult(false, false, false);
        }
    }

    internal static ThumbnailDecodeStrategy? ParseStrategy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<ThumbnailDecodeStrategy>(value, ignoreCase: true, out var strategy)
            ? strategy
            : null;
    }

    private void EnsureMachineBinding()
    {
        string machineId = _machineIdProvider();
        var settings = _settings.Load();
        if (string.Equals(settings.ThumbnailDecoderMachineId, machineId, StringComparison.Ordinal))
            return;

        settings.ThumbnailDecoderMachineId = machineId;
        settings.ThumbnailPreferredDecoder = string.Empty;
        _settings.Save();
        Log.Info("Thumbnail decode machine binding changed; cleared cached preferred decoder");
    }

    private void EnsureProbe()
    {
        if (_probeInitialized)
            return;

        _probe = _probeFactory();
        _probeInitialized = true;
        Log.Info(
            $"Thumbnail hardware probe: cuda={_probe.SupportsCuda}, qsv={_probe.SupportsQsv}, d3d11va={_probe.SupportsD3D11VA}");
    }

    private ThumbnailDecodeStatusSnapshot BuildStatusSnapshot()
    {
        EnsureMachineBinding();
        EnsureProbe();

        var settings = _settings.Load();
        var strategies = new List<ThumbnailDecodeStrategy>(5);
        AddStrategiesForMode(strategies);
        AddIfDistinct(strategies, ThumbnailDecodeStrategy.Software);

        return new ThumbnailDecodeStatusSnapshot(
            _accelerationMode,
            strategies,
            ParseStrategy(settings.ThumbnailPreferredDecoder),
            _probe.SupportsCuda,
            _probe.SupportsQsv,
            _probe.SupportsD3D11VA);
    }

    private void AddStrategiesForMode(ICollection<ThumbnailDecodeStrategy> strategies)
    {
        if (_accelerationMode == ThumbnailAccelerationMode.Compatible)
        {
            if (_probe.SupportsD3D11VA)
                AddIfDistinct(strategies, ThumbnailDecodeStrategy.D3D11VA);

            AddIfDistinct(strategies, ThumbnailDecodeStrategy.AutoHardware);
            return;
        }

        AddIfDistinct(strategies, ParseStrategy(_settings.Load().ThumbnailPreferredDecoder));

        if (_probe.SupportsCuda)
            AddIfDistinct(strategies, ThumbnailDecodeStrategy.NvidiaCuda);

        if (_probe.SupportsQsv)
            AddIfDistinct(strategies, ThumbnailDecodeStrategy.IntelQsv);

        if (_probe.SupportsD3D11VA)
            AddIfDistinct(strategies, ThumbnailDecodeStrategy.D3D11VA);

        AddIfDistinct(strategies, ThumbnailDecodeStrategy.AutoHardware);
    }

    private static void AddIfDistinct(ICollection<ThumbnailDecodeStrategy> strategies, ThumbnailDecodeStrategy? strategy)
    {
        if (strategy is null)
            return;

        if (!strategies.Contains(strategy.Value))
            strategies.Add(strategy.Value);
    }
}
