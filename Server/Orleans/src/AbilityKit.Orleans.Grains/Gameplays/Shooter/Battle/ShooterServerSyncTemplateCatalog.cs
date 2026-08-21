using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;

internal sealed class ShooterServerSyncTemplatePolicy
{
    public ShooterServerSyncTemplatePolicy(
        string templateId,
        int snapshotIntervalFrames,
        int fullSnapshotIntervalFrames,
        ShooterStateSyncPushPayloadMode payloadMode,
        NetworkConditionProfile defaultNetworkCondition,
        ShooterPureStateSyncSettings? pureStateSettings = null,
        float aoiVisibleRadius = 24f,
        float aoiBoundaryRadius = 30f,
        bool useObserverAoi = false,
        ShooterPureStatePlaybackPayloadMode playbackPayloadMode = ShooterPureStatePlaybackPayloadMode.SingleSample,
        int sampleBlockFrameCount = 1,
        ShooterPureStateSampleDensityPolicy? sampleDensityPolicy = null)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template id is required.", nameof(templateId));
        }

        TemplateId = templateId;
        SnapshotIntervalFrames = Math.Max(1, snapshotIntervalFrames);
        FullSnapshotIntervalFrames = Math.Max(SnapshotIntervalFrames, fullSnapshotIntervalFrames);
        PayloadMode = payloadMode;
        DefaultNetworkCondition = defaultNetworkCondition;
        PureStateSettings = pureStateSettings;
        AoiVisibleRadius = Math.Max(0f, aoiVisibleRadius);
        AoiBoundaryRadius = Math.Max(AoiVisibleRadius, aoiBoundaryRadius);
        UseObserverAoi = useObserverAoi;
        PlaybackPayloadMode = playbackPayloadMode;
        SampleBlockFrameCount = sampleBlockFrameCount;
        SampleDensityPolicy = sampleDensityPolicy ?? ShooterPureStateSampleDensityPolicy.FullDensity;
    }

    public string TemplateId { get; }

    public int SnapshotIntervalFrames { get; }

    public int FullSnapshotIntervalFrames { get; }

    public ShooterStateSyncPushPayloadMode PayloadMode { get; }

    public NetworkConditionProfile DefaultNetworkCondition { get; }

    public ShooterPureStateSyncSettings? PureStateSettings { get; }

    public float AoiVisibleRadius { get; }

    public float AoiBoundaryRadius { get; }

    public bool UseObserverAoi { get; }

    public ShooterPureStatePlaybackPayloadMode PlaybackPayloadMode { get; }

    public int SampleBlockFrameCount { get; }

    public ShooterPureStateSampleDensityPolicy SampleDensityPolicy { get; }

    public ServerBattleSyncTemplate CreateServerTemplate()
    {
        return new ServerBattleSyncTemplate(
            TemplateId,
            ServerBattleSyncMode.StateSync,
            ServerBattleRuntimeMode.BattleWorld,
            SnapshotIntervalFrames,
            FullSnapshotIntervalFrames);
    }

    public ShooterStateSyncPushOptions CreatePushOptions(string? networkEnvironmentId)
    {
        var networkCondition = ShooterServerSyncTemplateCatalog.ResolveNetworkCondition(
            networkEnvironmentId,
            DefaultNetworkCondition);
        return PayloadMode == ShooterStateSyncPushPayloadMode.PureState
            ? ShooterStateSyncPushOptions.PureState(
                networkCondition,
                PureStateSettings,
                AoiVisibleRadius,
                AoiBoundaryRadius,
                UseObserverAoi,
                PlaybackPayloadMode,
                SampleBlockFrameCount,
                SampleDensityPolicy)
            : ShooterStateSyncPushOptions.Packed(networkCondition);
    }
}

internal static class ShooterServerSyncTemplateCatalog
{
    private static readonly ShooterPureStateSyncSettings BatchStateSettings = new(
        maxEntityCount: 10000,
        activeSyncBudget: 1024,
        baselineIntervalFrames: 300,
        deltaIntervalFrames: 60,
        lowFrequencyIntervalFrames: 60,
        interpolationDelayFrames: 60);

    private static readonly ShooterPureStateSyncSettings MassBattleSettings = new(
        maxEntityCount: 20000,
        activeSyncBudget: 2048,
        baselineIntervalFrames: 450,
        deltaIntervalFrames: 3,
        lowFrequencyIntervalFrames: 30,
        interpolationDelayFrames: 3,
        nearLodIntervalFrames: 3,
        midLodIntervalFrames: 9,
        farLodIntervalFrames: 30);

    private static readonly IReadOnlyList<ShooterServerSyncTemplatePolicy> Policies = new[]
    {
        // Predicted clients replay from each authoritative snapshot. Packed deltas are relative
        // to the server baseline and cannot be imported into a world already predicted ahead.
        Packed(ShooterServerProtocol.PredictRollbackAuthorityTemplate, 1, 1, NetworkConditionProfile.Ideal),
        Packed(ShooterServerProtocol.AuthoritativeInterpolationPresentationTemplate, 1, 60, NetworkConditionProfile.Lan),
        PureState(ShooterServerProtocol.BatchStateLowFrequencyTemplate, 60, 300, NetworkConditionProfile.Mobile4G, BatchStateSettings),
        PureState(ShooterServerProtocol.MassBattleLodAoiTemplate, 3, 450, NetworkConditionProfile.LimitedBandwidth, MassBattleSettings, 24f, 30f, useObserverAoi: true),
        PureState(ShooterServerProtocol.MassBattleLodAoiSampleBlockTemplate, 3, 450, NetworkConditionProfile.LimitedBandwidth, MassBattleSettings, 24f, 30f, useObserverAoi: true, playbackPayloadMode: ShooterPureStatePlaybackPayloadMode.MultiSampleBlock, sampleBlockFrameCount: 3, sampleDensityPolicy: ShooterPureStateSampleDensityPolicy.MassBattle),
        Packed(ShooterServerProtocol.HybridHeroPredictionTemplate, 1, 30, NetworkConditionProfile.Lan),
        Packed(ShooterServerProtocol.RuntimeSnapshotInterpolationTemplate, 1, 60, NetworkConditionProfile.Lan),
        Packed(ShooterServerProtocol.StateSyncAuthorityTemplate, 1, 30, NetworkConditionProfile.Ideal),
        PureState(ShooterServerProtocol.PureStateAuthorityTemplate, 1, 60, NetworkConditionProfile.Ideal, settings: null)
    };

    public static ShooterServerSyncTemplatePolicy Default =>
        Resolve(ShooterServerProtocol.StateSyncAuthorityTemplate);

    public static ServerBattleSyncProfile CreateSyncProfile()
    {
        var defaultPolicy = Default;
        var defaultTemplate = defaultPolicy.CreateServerTemplate();
        var additional = new List<ServerBattleSyncTemplate>(Policies.Count - 1);
        for (var i = 0; i < Policies.Count; i++)
        {
            if (!ReferenceEquals(Policies[i], defaultPolicy))
            {
                additional.Add(Policies[i].CreateServerTemplate());
            }
        }

        return ServerBattleSyncProfile.FromTemplates(defaultTemplate, additional.ToArray());
    }

    public static ShooterServerSyncTemplatePolicy Resolve(string? templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Default;
        }

        for (var i = 0; i < Policies.Count; i++)
        {
            if (string.Equals(Policies[i].TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            {
                return Policies[i];
            }
        }

        throw new InvalidOperationException($"Unsupported Shooter sync template. TemplateId={templateId}");
    }

    public static NetworkConditionProfile ResolveNetworkCondition(
        string? networkEnvironmentId,
        NetworkConditionProfile fallback)
    {
        return networkEnvironmentId?.Trim().ToLowerInvariant() switch
        {
            "ideal" or "server-sandbox" => NetworkConditionProfile.Ideal,
            "lan" => NetworkConditionProfile.Lan,
            "mobile4g" => NetworkConditionProfile.Mobile4G,
            "crossregion" => NetworkConditionProfile.CrossRegion,
            "poorwifi" => NetworkConditionProfile.PoorWifi,
            "limitedbw" => NetworkConditionProfile.LimitedBandwidth,
            _ => fallback
        };
    }

    private static ShooterServerSyncTemplatePolicy Packed(
        string templateId,
        int snapshotIntervalFrames,
        int fullSnapshotIntervalFrames,
        NetworkConditionProfile networkCondition)
    {
        return new ShooterServerSyncTemplatePolicy(
            templateId,
            snapshotIntervalFrames,
            fullSnapshotIntervalFrames,
            ShooterStateSyncPushPayloadMode.Packed,
            networkCondition);
    }

    private static ShooterServerSyncTemplatePolicy PureState(
        string templateId,
        int snapshotIntervalFrames,
        int fullSnapshotIntervalFrames,
        NetworkConditionProfile networkCondition,
        ShooterPureStateSyncSettings? settings,
        float aoiVisibleRadius = 24f,
        float aoiBoundaryRadius = 30f,
        bool useObserverAoi = false,
        ShooterPureStatePlaybackPayloadMode playbackPayloadMode = ShooterPureStatePlaybackPayloadMode.SingleSample,
        int sampleBlockFrameCount = 1,
        ShooterPureStateSampleDensityPolicy? sampleDensityPolicy = null)
    {
        return new ShooterServerSyncTemplatePolicy(
            templateId,
            snapshotIntervalFrames,
            fullSnapshotIntervalFrames,
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
}
