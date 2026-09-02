using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;
using AbilityKit.Orleans.Contracts.Automation;
using AbilityKit.Orleans.Contracts.Rooms;
using Orleans.Serialization;

namespace AbilityKit.Orleans.Gateway.HttpApi;

[GenerateSerializer]
internal sealed record AccountLoginHttpRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] int ExpireSeconds = 0,
    [property: Id(2)] bool KickExisting = false);

[GenerateSerializer]
internal sealed record SessionTokenHttpRequest(
    [property: Id(0)] string SessionToken);

[GenerateSerializer]
internal sealed record RenewSessionHttpRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] int ExtendSeconds = 0,
    [property: Id(2)] bool RotateToken = false);

[GenerateSerializer]
internal sealed record MarkRoomMemberOfflineRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Reason);

[GenerateSerializer]
internal sealed record GameplayHttpDescriptor(
    [property: Id(0)] string RoomType,
    [property: Id(1)] int GameplayId,
    [property: Id(2)] string DisplayName,
    [property: Id(3)] int DefaultMaxPlayers,
    [property: Id(4)] bool RequiresPlayerLoadout,
    [property: Id(5)] string? DefaultWorldType,
    [property: Id(6)] int DefaultTickRate,
    [property: Id(7)] string? DefaultSyncTemplateId,
    [property: Id(8)] string[] SupportedSyncTemplateIds,
    [property: Id(9)] bool SupportsStateSyncPush,
    [property: Id(10)] bool SupportsFrameSync);

[GenerateSerializer]
internal sealed record WebCreateRoomRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] string RoomType,
    [property: Id(4)] string Title,
    [property: Id(5)] bool IsPublic,
    [property: Id(6)] int MaxPlayers,
    [property: Id(7)] Dictionary<string, string>? Tags,
    [property: Id(8)] bool AutoJoin = true);

[GenerateSerializer]
internal sealed record WebListRoomsRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] int Offset,
    [property: Id(4)] int Limit,
    [property: Id(5)] string? RoomType);

[GenerateSerializer]
internal sealed record WebRoomRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] string RoomId);

[GenerateSerializer]
internal sealed record WebRoomReadyRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] string RoomId,
    [property: Id(2)] bool Ready);

[GenerateSerializer]
internal sealed record WebRoomPickHeroRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] string RoomId,
    [property: Id(2)] int HeroId,
    [property: Id(3)] int TeamId,
    [property: Id(4)] int SpawnPointId,
    [property: Id(5)] int Level,
    [property: Id(6)] int AttributeTemplateId,
    [property: Id(7)] int BasicAttackSkillId,
    [property: Id(8)] List<int>? SkillIds);

[GenerateSerializer]
internal sealed record WebAddRoomRobotsRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] string RoomId,
    [property: Id(2)] int Count,
    [property: Id(3)] string? AccountPrefix,
    [property: Id(4)] bool AutoReady = true,
    [property: Id(5)] bool MountBattleAi = true,
    [property: Id(6)] string? BattleAiProfileId = "simple-battle");

[GenerateSerializer]
internal sealed record WebStartRoomBattleRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] string RoomId,
    [property: Id(2)] int GameplayId,
    [property: Id(3)] int RuleSetId,
    [property: Id(4)] int ConfigVersion,
    [property: Id(5)] int ProtocolVersion,
    [property: Id(6)] string? WorldType,
    [property: Id(7)] string? ClientId,
    [property: Id(8)] string? SyncTemplateId,
    [property: Id(9)] int? SyncModel,
    [property: Id(10)] string? NetworkEnvironmentId,
    [property: Id(11)] string? CarrierName,
    [property: Id(12)] bool EnableAuthoritativeWorld,
    [property: Id(13)] bool InterpolationEnabled,
    [property: Id(14)] int InputDelayFrames);

[GenerateSerializer]
internal sealed record WebStartRoomBattleResponse(
    [property: Id(0)] StartRoomBattleResponse Start,
    [property: Id(1)] MountRoomRobotBattleAiResponse? BattleAiMount);

[GenerateSerializer]
internal sealed record ShooterSandboxHttpStartRequest(
    [property: Id(0)] string? SandboxId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] int BotCount,
    [property: Id(4)] int MaxPlayers,
    [property: Id(5)] int TickRate,
    [property: Id(6)] string? Title,
    [property: Id(7)] Dictionary<string, string>? Tags);

[GenerateSerializer]
internal sealed record ShooterSandboxHttpRequest(
    [property: Id(0)] string? SandboxId,
    [property: Id(1)] string? ServerId);

[GenerateSerializer]
internal sealed record AdminDashboardHttpRequest(
    [property: Id(0)] string? SessionToken,
    [property: Id(1)] string? Region,
    [property: Id(2)] string? ServerId,
    [property: Id(3)] string? RoomType,
    [property: Id(4)] int Limit = 20,
    [property: Id(5)] string? SandboxId = null);

[GenerateSerializer]
internal sealed record AdminDashboardHttpResponse(
    [property: Id(0)] GameplayHttpDescriptor[] Gameplays,
    [property: Id(1)] List<RoomSummary> Rooms,
    [property: Id(2)] string? AccountId,
    [property: Id(3)] string? CurrentRoomId,
    [property: Id(4)] RestoreRoomResponse? CurrentRoom,
    [property: Id(5)] RoomRuntimeState? RuntimeState,
    [property: Id(6)] ShooterSandboxState? ShooterSandbox,
    [property: Id(7)] long ServerNowTicks,
    [property: Id(8)] AdminServerStatusHttpResponse? ServerStatus = null);

[GenerateSerializer]
internal sealed record AdminServerStatusHttpResponse(
    [property: Id(0)] string EnvironmentName,
    [property: Id(1)] string ApplicationName,
    [property: Id(2)] string MachineName,
    [property: Id(3)] int ProcessId,
    [property: Id(4)] string ProcessName,
    [property: Id(5)] long StartTimeUtcTicks,
    [property: Id(6)] long UptimeSeconds,
    [property: Id(7)] long WorkingSetBytes,
    [property: Id(8)] long GcTotalMemoryBytes,
    [property: Id(9)] int ThreadCount,
    [property: Id(10)] bool MaintenanceMode,
    [property: Id(11)] bool DrainMode,
    [property: Id(12)] bool RestartRequested,
    [property: Id(13)] string? LastOperationId,
    [property: Id(14)] string? LastOperationReason,
    [property: Id(15)] string? LastOperationRequestedBy,
    [property: Id(16)] long? LastOperationRequestedAtTicks,
    [property: Id(17)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminClusterDiagnosticsHttpResponse(
    [property: Id(0)] string ClusterId,
    [property: Id(1)] string ServiceId,
    [property: Id(2)] int? SiloPort,
    [property: Id(3)] int? OrleansGatewayPort,
    [property: Id(4)] bool ClientConnected,
    [property: Id(5)] string ClientStatus,
    [property: Id(6)] AdminClusterNodeProbeHttpResponse[] Nodes,
    [property: Id(7)] string[] RuntimeMetrics,
    [property: Id(8)] string[] Warnings,
    [property: Id(9)] AdminServerStatusHttpResponse GatewayProcess,
    [property: Id(10)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminClusterNodeProbeHttpResponse(
    [property: Id(0)] string NodeId,
    [property: Id(1)] string Role,
    [property: Id(2)] string Endpoint,
    [property: Id(3)] string Status,
    [property: Id(4)] string Diagnostics);

[GenerateSerializer]
internal sealed record AdminSkillDiagnosticsSummaryHttpResponse(
    [property: Id(0)] string? RoomId,
    [property: Id(1)] string? RoomType,
    [property: Id(2)] string? BattleId,
    [property: Id(3)] ulong WorldId,
    [property: Id(4)] bool IsInBattle,
    [property: Id(5)] int CurrentFrame,
    [property: Id(6)] string[] Members,
    [property: Id(7)] string DiagnosticsStatus,
    [property: Id(8)] AdminSkillActorSummaryHttpResponse[] Actors,
    [property: Id(9)] AdminSkillMetricHttpResponse[] Metrics,
    [property: Id(10)] string[] Warnings,
    [property: Id(11)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillActorSummaryHttpResponse(
    [property: Id(0)] string AccountId,
    [property: Id(1)] int ActorId,
    [property: Id(2)] int BasicAttackSkillId,
    [property: Id(3)] int[] SkillIds,
    [property: Id(4)] string Diagnostics);

[GenerateSerializer]
internal sealed record AdminSkillMetricHttpResponse(
    [property: Id(0)] string Name,
    [property: Id(1)] double Value,
    [property: Id(2)] string Unit,
    [property: Id(3)] string Source);

[GenerateSerializer]
internal sealed record AdminSkillDiagnosticsEventsHttpResponse(
    [property: Id(0)] string DiagnosticsStatus,
    [property: Id(1)] AdminSkillEventFilterHttpResponse Filters,
    [property: Id(2)] AdminSkillEventHttpResponse[] Events,
    [property: Id(3)] string[] Warnings,
    [property: Id(4)] long ServerNowTicks,
    [property: Id(5)] string SchemaVersion,
    [property: Id(6)] bool IsDataAvailable,
    [property: Id(7)] string? UnavailableReason,
    [property: Id(8)] long StoreRevision = 0,
    [property: Id(9)] bool HasMore = false,
    [property: Id(10)] long MonotonicTimestampFrequency = 0,
    [property: Id(11)] int Offset = 0,
    [property: Id(12)] int Limit = 0,
    [property: Id(13)] string? DataAvailability = null);

[GenerateSerializer]
internal sealed record AdminSkillEventFilterHttpResponse(
    [property: Id(0)] string? BattleId,
    [property: Id(1)] int? ActorId,
    [property: Id(2)] int? SkillId,
    [property: Id(3)] int Limit);

[GenerateSerializer]
internal sealed record AdminSkillEventHttpResponse(
    [property: Id(0)] int Frame,
    [property: Id(1)] int ActorId,
    [property: Id(2)] int SkillId,
    [property: Id(3)] long SkillInstanceId,
    [property: Id(4)] string Stage,
    [property: Id(5)] string EventType,
    [property: Id(6)] int? TargetActorId,
    [property: Id(7)] double? Value,
    [property: Id(8)] string? Message,
    [property: Id(9)] string Severity,
    [property: Id(10)] string BattleId,
    [property: Id(11)] string WorldId,
    [property: Id(12)] string SessionId,
    [property: Id(13)] int Generation,
    [property: Id(14)] long Sequence,
    [property: Id(15)] long MonotonicTimestamp,
    [property: Id(16)] long NodeId,
    [property: Id(17)] long RootId,
    [property: Id(18)] long ParentId,
    [property: Id(19)] long SourceContextId,
    [property: Id(20)] long RootContextId,
    [property: Id(21)] long OwnerContextId);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisModelHttpResponse(
    [property: Id(0)] string ModelVersion,
    [property: Id(1)] string[] Sources,
    [property: Id(2)] AdminSkillAnalysisStageHttpResponse[] Stages,
    [property: Id(3)] AdminSkillAnalysisFieldHttpResponse[] CorrelationFields,
    [property: Id(4)] AdminSkillAnalysisProjectionSchemaHttpResponse[] ProjectionSchemas,
    [property: Id(5)] string[] Notes,
    [property: Id(6)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisArtifactDirectoryHttpResponse(
    [property: Id(0)] string ArtifactDirectory,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] bool Exists,
    [property: Id(3)] int ArtifactCount,
    [property: Id(4)] long LastWriteUtcTicks);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisArtifactDirectoryListHttpResponse(
    [property: Id(0)] string ArtifactRootDirectory,
    [property: Id(1)] AdminSkillAnalysisArtifactDirectoryHttpResponse[] Directories,
    [property: Id(2)] string[] Warnings,
    [property: Id(3)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisArtifactListItemHttpResponse(
    [property: Id(0)] string FileName,
    [property: Id(1)] string SessionId,
    [property: Id(2)] string SchemaVersion,
    [property: Id(3)] string? Project,
    [property: Id(4)] string? Scenario,
    [property: Id(5)] int RootCount,
    [property: Id(6)] int NodeCount,
    [property: Id(7)] int StartFrame,
    [property: Id(8)] int EndFrame,
    [property: Id(9)] long GeneratedAtUtcTicks,
    [property: Id(10)] long FileSizeBytes,
    [property: Id(11)] string Path);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisArtifactListHttpResponse(
    [property: Id(0)] string ArtifactDirectory,
    [property: Id(1)] AdminSkillAnalysisArtifactListItemHttpResponse[] Artifacts,
    [property: Id(2)] string[] Warnings,
    [property: Id(3)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisArtifactHttpResponse(
    [property: Id(0)] string ArtifactDirectory,
    [property: Id(1)] string FileName,
    [property: Id(2)] string Path,
    [property: Id(3)] JsonNode? Artifact,
    [property: Id(4)] string[] Warnings,
    [property: Id(5)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisStageHttpResponse(
    [property: Id(0)] string Id,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string RuntimeSource,
    [property: Id(3)] string AcceptanceSource,
    [property: Id(4)] string[] Fields);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisFieldHttpResponse(
    [property: Id(0)] string Name,
    [property: Id(1)] string Description,
    [property: Id(2)] bool RequiredForCorrelation);

[GenerateSerializer]
internal sealed record AdminSkillAnalysisProjectionSchemaHttpResponse(
    [property: Id(0)] string Id,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string Description,
    [property: Id(3)] string[] Fields);

[GenerateSerializer]
internal sealed record AdminApiErrorHttpResponse(
    [property: Id(0)] string Code,
    [property: Id(1)] string Message,
    [property: Id(2)] string Target,
    [property: Id(3)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceBatchHttpResponse(
    [property: Id(0)] string ArtifactDirectory,
    [property: Id(1)] bool HasBatchSummary,
    [property: Id(2)] JsonNode? BatchSummary,
    [property: Id(3)] AdminSkillAcceptanceCaseListItemHttpResponse[] Cases,
    [property: Id(4)] string[] Warnings,
    [property: Id(5)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceCaseListItemHttpResponse(
    [property: Id(0)] string CaseId,
    [property: Id(1)] string? Description,
    [property: Id(2)] string? WorldId,
    [property: Id(3)] int TickRate,
    [property: Id(4)] bool Accelerated,
    [property: Id(5)] bool? Passed,
    [property: Id(6)] int FinalFrame,
    [property: Id(7)] int FinalTimeMs,
    [property: Id(8)] int TraceNodeCount,
    [property: Id(9)] string SummaryPath,
    [property: Id(10)] string TracePath,
    [property: Id(11)] string? Category,
    [property: Id(12)] string[] Tags,
    [property: Id(13)] string? GeneratedFrom,
    [property: Id(14)] string? LastReviewedAt,
    [property: Id(15)] string? MissingTraceNodes,
    [property: Id(16)] string? UnexpectedTraceNodes,
    [property: Id(17)] string? MissingActions,
    [property: Id(18)] string? MissingRelationships);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceCaseHttpResponse(
    [property: Id(0)] string CaseId,
    [property: Id(1)] string ArtifactDirectory,
    [property: Id(2)] JsonNode? Summary,
    [property: Id(3)] JsonNode[] TraceRecords,
    [property: Id(4)] string SummaryPath,
    [property: Id(5)] string TracePath,
    [property: Id(6)] string[] Warnings,
    [property: Id(7)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceDeleteRequest(
    [property: Id(0)] string? SessionToken,
    [property: Id(1)] string? ArtifactDirectory,
    [property: Id(2)] string[] CaseIds,
    [property: Id(3)] string? OperatorReason);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceDeleteResponse(
    [property: Id(0)] bool Success,
    [property: Id(1)] string ArtifactDirectory,
    [property: Id(2)] string[] DeletedCaseIds,
    [property: Id(3)] string[] DeletedPaths,
    [property: Id(4)] string[] MissingCaseIds,
    [property: Id(5)] string[] Warnings,
    [property: Id(6)] AdminSkillAcceptanceBatchHttpResponse Batch,
    [property: Id(7)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceArtifactDirectoryHttpResponse(
    [property: Id(0)] string ArtifactDirectory,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] bool Exists,
    [property: Id(3)] bool HasBatchSummary,
    [property: Id(4)] int CaseCount,
    [property: Id(5)] long LastWriteUtcTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceArtifactDirectoryListHttpResponse(
    [property: Id(0)] string ArtifactRootDirectory,
    [property: Id(1)] AdminSkillAcceptanceArtifactDirectoryHttpResponse[] Directories,
    [property: Id(2)] string[] Warnings,
    [property: Id(3)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceRunRequest(
    [property: Id(0)] string? SessionToken,
    [property: Id(1)] string? ArtifactDirectory,
    [property: Id(2)] string? CaseId,
    [property: Id(3)] string? Description,
    [property: Id(4)] int ActorId,
    [property: Id(5)] int TargetActorId,
    [property: Id(6)] int SkillId,
    [property: Id(7)] int EffectId,
    [property: Id(8)] int ProjectileId,
    [property: Id(9)] int AreaId,
    [property: Id(10)] int BuffId,
    [property: Id(11)] int ShieldId,
    [property: Id(12)] int BaseDamage,
    [property: Id(13)] int MitigatedDamage,
    [property: Id(14)] int ShieldAbsorb,
    [property: Id(15)] int HpDamage,
    [property: Id(16)] int TickRate,
    [property: Id(17)] int DurationFrames,
    [property: Id(18)] string? TemplateId,
    [property: Id(19)] string? OperatorReason);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceRunResponse(
    [property: Id(0)] bool Success,
    [property: Id(1)] string OperationId,
    [property: Id(2)] string ArtifactDirectory,
    [property: Id(3)] string CaseId,
    [property: Id(4)] string SummaryPath,
    [property: Id(5)] string TracePath,
    [property: Id(6)] AdminSkillAcceptanceBatchHttpResponse Batch,
    [property: Id(7)] string[] Warnings,
    [property: Id(8)] long ServerNowTicks,
    [property: Id(9)] string ScenarioId,
    [property: Id(10)] string ExecutionStatus,
    [property: Id(11)] int ExitCode,
    [property: Id(12)] string LogPath,
    [property: Id(13)] string ExecutionResultPath,
    [property: Id(14)] string StartedAtUtc,
    [property: Id(15)] string EndedAtUtc,
    [property: Id(16)] int DurationMs);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceTemplateHttpResponse(
    [property: Id(0)] string Id,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string Description,
    [property: Id(3)] string[] Covers,
    [property: Id(4)] AdminSkillAcceptanceRunRequest Defaults);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceTemplateListHttpResponse(
    [property: Id(0)] AdminSkillAcceptanceTemplateHttpResponse[] Templates,
    [property: Id(1)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceRunPlanHttpResponse(
    [property: Id(0)] bool Allowed,
    [property: Id(1)] string Message,
    [property: Id(2)] string ArtifactDirectory,
    [property: Id(3)] string ExecutionMode,
    [property: Id(4)] bool CanRequestFromAdmin,
    [property: Id(5)] AdminSkillAcceptanceExecutionStrategyHttpResponse[] Strategies,
    [property: Id(6)] AdminSkillAcceptanceAllowedScriptHttpResponse[] AllowedScripts,
    [property: Id(7)] string[] RequiredApprovals,
    [property: Id(8)] string[] AuditFields,
    [property: Id(9)] string[] Notes,
    [property: Id(10)] long ServerNowTicks);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceExecutionStrategyHttpResponse(
    [property: Id(0)] string Id,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string Boundary,
    [property: Id(3)] string Status,
    [property: Id(4)] string Description);

[GenerateSerializer]
internal sealed record AdminSkillAcceptanceAllowedScriptHttpResponse(
    [property: Id(0)] string Id,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string RelativePath,
    [property: Id(3)] string Shell,
    [property: Id(4)] bool Exists,
    [property: Id(5)] string[] Arguments,
    [property: Id(6)] string[] Produces);

[GenerateSerializer]
internal sealed record AdminServerOperationHttpRequest(
    [property: Id(0)] string? SessionToken,
    [property: Id(1)] string? Reason,
    [property: Id(2)] bool Enabled = true,
    [property: Id(3)] string? OperationId = null);

[GenerateSerializer]
internal sealed record AdminServerOperationHttpResponse(
    [property: Id(0)] bool Success,
    [property: Id(1)] string Operation,
    [property: Id(2)] string OperationId,
    [property: Id(3)] string RequestedBy,
    [property: Id(4)] string? Reason,
    [property: Id(5)] AdminServerStatusHttpResponse Status);
