using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;

internal enum ShooterStateSyncPushPayloadMode
{
    Packed = 0,
    PureState = 1
}

internal enum ShooterPureStatePlaybackPayloadMode
{
    SingleSample = 0,
    MultiSampleBlock = 1
}

internal readonly struct ShooterPureStateSampleDensityPolicy
{
    public ShooterPureStateSampleDensityPolicy(
        float nearRadiusRatio,
        float midRadiusRatio,
        int nearHistoricalStride,
        int midHistoricalStride,
        int farHistoricalStride,
        int maxHistoricalTransformsPerBlock = int.MaxValue)
    {
        NearRadiusRatio = Math.Clamp(nearRadiusRatio, 0f, 1f);
        MidRadiusRatio = Math.Clamp(midRadiusRatio, NearRadiusRatio, 1f);
        NearHistoricalStride = Math.Max(0, nearHistoricalStride);
        MidHistoricalStride = Math.Max(0, midHistoricalStride);
        FarHistoricalStride = Math.Max(0, farHistoricalStride);
        MaxHistoricalTransformsPerBlock = Math.Max(0, maxHistoricalTransformsPerBlock);
    }

    public float NearRadiusRatio { get; }

    public float MidRadiusRatio { get; }

    public int NearHistoricalStride { get; }

    public int MidHistoricalStride { get; }

    public int FarHistoricalStride { get; }

    public int MaxHistoricalTransformsPerBlock { get; }

    public static ShooterPureStateSampleDensityPolicy FullDensity { get; } = new(
        1f,
        1f,
        nearHistoricalStride: 1,
        midHistoricalStride: 1,
        farHistoricalStride: 1);

    public static ShooterPureStateSampleDensityPolicy MassBattle { get; } = new(
        0.40f,
        0.75f,
        nearHistoricalStride: 1,
        midHistoricalStride: 2,
        farHistoricalStride: 0,
        maxHistoricalTransformsPerBlock: 32);

    public static ShooterPureStateSampleDensityPolicy SmoothMassBattle { get; } = new(
        0.40f,
        0.75f,
        nearHistoricalStride: 1,
        midHistoricalStride: 2,
        farHistoricalStride: 2,
        maxHistoricalTransformsPerBlock: 2048);
}

internal sealed class ShooterStateSyncPushOptions
{
    public const string PayloadModeEnvironmentVariable = "ABILITYKIT_SHOOTER_STATE_SYNC_PAYLOAD_MODE";

    private const int LimitedBandwidthKbps = 256;
    private const int HighLatencyMs = 120;
    private const double LossyLinkRate = 0.02d;

    private ShooterStateSyncPushOptions(
        ShooterStateSyncPushPayloadMode payloadMode,
        NetworkConditionProfile networkCondition,
        ShooterPureStateSyncSettings? pureStateSettings,
        float aoiVisibleRadius,
        float aoiBoundaryRadius,
        bool useObserverAoi,
        ShooterPureStatePlaybackPayloadMode playbackPayloadMode,
        int sampleBlockFrameCount,
        ShooterPureStateSampleDensityPolicy? sampleDensityPolicy)
    {
        PayloadMode = payloadMode;
        NetworkCondition = networkCondition;
        PureStateSettings = pureStateSettings;
        AoiVisibleRadius = aoiVisibleRadius > 0f ? aoiVisibleRadius : 24f;
        AoiBoundaryRadius = aoiBoundaryRadius >= AoiVisibleRadius ? aoiBoundaryRadius : AoiVisibleRadius;
        UseObserverAoi = useObserverAoi;
        PlaybackPayloadMode = playbackPayloadMode;
        SampleBlockFrameCount = playbackPayloadMode == ShooterPureStatePlaybackPayloadMode.MultiSampleBlock
            ? Math.Max(2, Math.Min(8, sampleBlockFrameCount))
            : 1;
        SampleDensityPolicy = sampleDensityPolicy ?? ShooterPureStateSampleDensityPolicy.FullDensity;
    }

    public ShooterStateSyncPushPayloadMode PayloadMode { get; }

    public NetworkConditionProfile NetworkCondition { get; }

    public ShooterPureStateSyncSettings? PureStateSettings { get; }

    public float AoiVisibleRadius { get; }

    public float AoiBoundaryRadius { get; }

    public bool UseObserverAoi { get; }

    public ShooterPureStatePlaybackPayloadMode PlaybackPayloadMode { get; }

    public int SampleBlockFrameCount { get; }

    public ShooterPureStateSampleDensityPolicy SampleDensityPolicy { get; }

    public static ShooterStateSyncPushOptions PackedDefault { get; } = new ShooterStateSyncPushOptions(
        ShooterStateSyncPushPayloadMode.Packed,
        NetworkConditionProfile.Ideal,
        null,
        24f,
        30f,
        false,
        ShooterPureStatePlaybackPayloadMode.SingleSample,
        1,
        null);

    public static ShooterStateSyncPushOptions Packed(NetworkConditionProfile networkCondition)
    {
        return new ShooterStateSyncPushOptions(
            ShooterStateSyncPushPayloadMode.Packed,
            networkCondition,
            null,
            24f,
            30f,
            false,
            ShooterPureStatePlaybackPayloadMode.SingleSample,
            1,
            null);
    }

    public static ShooterStateSyncPushOptions PureState(
        NetworkConditionProfile networkCondition,
        ShooterPureStateSyncSettings? settings = null,
        float aoiVisibleRadius = 24f,
        float aoiBoundaryRadius = 30f,
        bool useObserverAoi = true,
        ShooterPureStatePlaybackPayloadMode playbackPayloadMode = ShooterPureStatePlaybackPayloadMode.SingleSample,
        int sampleBlockFrameCount = 1,
        ShooterPureStateSampleDensityPolicy? sampleDensityPolicy = null)
    {
        return new ShooterStateSyncPushOptions(
            ShooterStateSyncPushPayloadMode.PureState,
            networkCondition,
            settings,
            aoiVisibleRadius,
            aoiBoundaryRadius,
            useObserverAoi,
            playbackPayloadMode,
            sampleBlockFrameCount,
            sampleDensityPolicy);
    }

    public static ShooterStateSyncPushOptions FromEnvironmentDefault()
    {
        return TryFromEnvironment(out var options) ? options : PackedDefault;
    }

    public static bool TryFromEnvironment(out ShooterStateSyncPushOptions options)
    {
        var value = Environment.GetEnvironmentVariable(PayloadModeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value) || !TryParsePayloadMode(value, out var payloadMode))
        {
            options = PackedDefault;
            return false;
        }

        options = payloadMode == ShooterStateSyncPushPayloadMode.PureState
            ? PureState(NetworkConditionProfile.Ideal)
            : PackedDefault;
        return true;
    }

    public static bool TryParsePayloadMode(string? value, out ShooterStateSyncPushPayloadMode payloadMode)
    {
        if (string.Equals(value, "pure-state", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "purestate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "pure_state", StringComparison.OrdinalIgnoreCase))
        {
            payloadMode = ShooterStateSyncPushPayloadMode.PureState;
            return true;
        }

        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "packed", StringComparison.OrdinalIgnoreCase))
        {
            payloadMode = ShooterStateSyncPushPayloadMode.Packed;
            return true;
        }

        payloadMode = ShooterStateSyncPushPayloadMode.Packed;
        return false;
    }

    public ShooterPureStateSyncSettings ResolvePureStateSettings()
    {
        if (PureStateSettings.HasValue)
        {
            return PureStateSettings.Value;
        }

        var defaults = ShooterPureStateSyncSettings.Default;
        if (NetworkCondition.BandwidthKbps > 0 && NetworkCondition.BandwidthKbps <= LimitedBandwidthKbps)
        {
            return new ShooterPureStateSyncSettings(
                defaults.MaxEntityCount,
                128,
                defaults.BaselineIntervalFrames,
                4,
                30,
                6);
        }

        if (NetworkCondition.PacketLossRate >= LossyLinkRate || NetworkCondition.JitterMs >= 50)
        {
            return new ShooterPureStateSyncSettings(
                defaults.MaxEntityCount,
                256,
                defaults.BaselineIntervalFrames,
                3,
                24,
                6);
        }

        if (NetworkCondition.BaseLatencyMs >= HighLatencyMs)
        {
            return new ShooterPureStateSyncSettings(
                defaults.MaxEntityCount,
                384,
                defaults.BaselineIntervalFrames,
                3,
                20,
                5);
        }

        return defaults;
    }
}
