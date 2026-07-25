namespace AbilityKit.Orleans.Contracts.Shooter;

public static class ShooterServerProtocol
{
    public const string RoomType = "shooter";
    public const string DefaultRegion = "dev";
    public const string DefaultServerId = "default";
    public const string DefaultSandboxId = DefaultServerId;
    public const string DefaultSandboxTitle = "Shooter Server Sandbox";
    public const string SandboxClientId = "server-shooter-sandbox";
    public const string SandboxNetworkEnvironmentId = "server-sandbox";
    public const string SandboxCarrierName = "server";
    public const string SandboxTagValue = RoomType;
    public const string RunningBattleLateJoinMode = "running-battle-late-join";

    public const string PredictRollbackAuthorityTemplate = "predict-rollback-authority";
    public const string AuthoritativeInterpolationPresentationTemplate = "authoritative-interpolation-presentation";
    public const string BatchStateLowFrequencyTemplate = "batch-state-low-frequency";
    public const string MassBattleLodAoiTemplate = "mass-battle-lod-aoi";
    public const string HybridHeroPredictionTemplate = "hybrid-hero-prediction";
    public const string RuntimeSnapshotInterpolationTemplate = "runtime-snapshot-interpolation";
    public const string StateSyncAuthorityTemplate = "state-sync-authority";
    public const string PureStateAuthorityTemplate = "pure-state-authority";

    public static string[] CreateStateSyncTemplateIds()
    {
        return new[]
        {
            PredictRollbackAuthorityTemplate,
            AuthoritativeInterpolationPresentationTemplate,
            BatchStateLowFrequencyTemplate,
            MassBattleLodAoiTemplate,
            HybridHeroPredictionTemplate,
            RuntimeSnapshotInterpolationTemplate,
            StateSyncAuthorityTemplate,
            PureStateAuthorityTemplate
        };
    }
}

public static class ShooterRoomTagKeys
{
    public const string TickRate = "tickRate";
    public const string MapId = "mapId";
    public const string RandomSeed = "randomSeed";
    public const string DurationFrames = "durationFrames";
    public const string VictoryTargetDefeats = "victoryTargetDefeats";
    public const string ContinueAfterAllPlayersDefeated = "continueAfterAllPlayersDefeated";
    public const string SyncTemplateId = "syncTemplateId";
    public const string SyncModel = "syncModel";
    public const string NetworkEnvironmentId = "networkEnvironmentId";
    public const string CarrierName = "carrierName";
    public const string EnableAuthoritativeWorld = "enableAuthoritativeWorld";
    public const string InterpolationEnabled = "interpolationEnabled";
    public const string InputDelayFrames = "inputDelayFrames";
    public const string Sandbox = "sandbox";
    public const string JoinMode = "joinMode";
}
