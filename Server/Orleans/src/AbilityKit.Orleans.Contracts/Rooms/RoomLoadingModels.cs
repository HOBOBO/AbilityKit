using System.Collections.Generic;
using Orleans.Serialization;

namespace AbilityKit.Orleans.Contracts.Rooms;

/// <summary>
/// Owner 发起的资源加载阶段请求。Lobby -> Loading。
/// </summary>
[GenerateSerializer]
public sealed record BeginLoadingRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] long? ExpectedRevision = null,
    [property: Id(2)] string? CommandId = null);

/// <summary>
/// 成员上报资源加载完成。
/// </summary>
[GenerateSerializer]
public sealed record ReportAssetsLoadedRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] long LaunchGeneration,
    [property: Id(2)] int ManifestVersion,
    [property: Id(3)] string? ManifestHash,
    [property: Id(4)] string? CommandId = null);

/// <summary>
/// Reports monotonic loading progress for the active launch generation.
/// Progress is observable room state; ReportAssetsLoaded remains the final manifest confirmation.
/// </summary>
[GenerateSerializer]
public sealed record ReportLoadingProgressRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] long LaunchGeneration,
    [property: Id(2)] int ManifestVersion,
    [property: Id(3)] string? ManifestHash,
    [property: Id(4)] int Progress);

/// <summary>
/// Owner 取消加载阶段，回到 Lobby。
/// </summary>
[GenerateSerializer]
public sealed record CancelLoadingRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] long? ExpectedRevision = null,
    [property: Id(2)] string? CommandId = null);

/// <summary>
/// Reminder / 测试驱动的时钟注入请求。
/// </summary>
[GenerateSerializer]
public sealed record RoomTickRequest(
    [property: Id(0)] long NowTicks,
    [property: Id(1)] long NowUnixMs,
    [property: Id(2)] long LoadingTimeoutMs = 0,
    [property: Id(3)] long OfflineGraceMs = 0);

/// <summary>
/// 冻结的启动清单。客户端据此加载资源；manifestHash 用于校验加载一致性。
/// </summary>
[GenerateSerializer]
public sealed record RoomLaunchManifest(
    [property: Id(0)] int ManifestVersion,
    [property: Id(1)] string ManifestHash,
    [property: Id(2)] List<string> AssetReferences,
    [property: Id(3)] Dictionary<string, string>? Metadata = null);
