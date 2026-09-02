namespace AbilityKit.Orleans.Gateway.HttpApi;

using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using Orleans;

internal static class GatewaySkillDiagnostics
{
    private const string RuntimeContextOnly = "RuntimeContextOnly";
    private const string RuntimeEventsUnavailable = "Unavailable";
    private const string RuntimeEventsSchemaVersion = "abilitykit-battle-diagnostics.v1";
    private const string RuntimeEventsUnavailableReason =
        "The battle runtime diagnostic read channel is not connected to the Gateway.";

    public static async Task<AdminSkillDiagnosticsSummaryHttpResponse> GetSummaryAsync(
        IClusterClient client,
        string? roomId,
        string? battleId)
    {
        RoomRuntimeState? runtimeState = null;
        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            try
            {
                var room = client.GetGrain<IRoomGrain>(roomId);
                runtimeState = await room.GetRuntimeStateAsync();
            }
            catch (Exception exception)
            {
                warnings.Add($"Room runtime state probe failed: {exception.Message}");
            }
        }

        var resolvedBattleId = !string.IsNullOrWhiteSpace(battleId) ? battleId : runtimeState?.BattleId;
        var currentFrame = 0;
        if (!string.IsNullOrWhiteSpace(resolvedBattleId))
        {
            try
            {
                var battle = client.GetGrain<IBattleLogicHostGrain>(resolvedBattleId);
                currentFrame = await battle.GetCurrentFrameAsync();
            }
            catch (Exception exception)
            {
                warnings.Add($"Battle frame probe failed: {exception.Message}");
            }
        }

        var members = runtimeState?.Members?.ToArray() ?? Array.Empty<string>();
        var actorSummaries = members
            .Select((member, index) => new AdminSkillActorSummaryHttpResponse(
                member,
                index + 1,
                0,
                Array.Empty<int>(),
                "Loadout details are not projected to skill diagnostics yet."))
            .ToArray();

        var metrics = new[]
        {
            new AdminSkillMetricHttpResponse("CastCount", 0, "count", RuntimeContextOnly),
            new AdminSkillMetricHttpResponse("RejectCount", 0, "count", RuntimeContextOnly),
            new AdminSkillMetricHttpResponse("FailureCount", 0, "count", RuntimeContextOnly),
            new AdminSkillMetricHttpResponse("DamageTotal", 0, "value", RuntimeContextOnly),
            new AdminSkillMetricHttpResponse("BuffApplyCount", 0, "count", RuntimeContextOnly),
            new AdminSkillMetricHttpResponse("AvgPipelineMs", 0, "ms", RuntimeContextOnly)
        };

        return new AdminSkillDiagnosticsSummaryHttpResponse(
            runtimeState?.RoomId ?? roomId,
            runtimeState?.RoomType,
            resolvedBattleId,
            runtimeState?.WorldId ?? 0UL,
            runtimeState?.IsInBattle ?? false,
            currentFrame,
            members,
            RuntimeContextOnly,
            actorSummaries,
            metrics,
            warnings.ToArray(),
            DateTime.UtcNow.Ticks);
    }

    public static AdminSkillAnalysisModelHttpResponse GetAnalysisModel()
    {
        return GatewaySkillAnalysisModelProvider.GetModel();
    }

    public static Task<AdminSkillDiagnosticsEventsHttpResponse> GetEventsAsync(
        string? battleId,
        int? actorId,
        int? skillId,
        int limit)
    {
        var effectiveLimit = limit <= 0 ? 100 : Math.Min(limit, 500);
        var filters = new AdminSkillEventFilterHttpResponse(battleId, actorId, skillId, effectiveLimit);
        return Task.FromResult(CreateUnavailableResponse(filters, RuntimeEventsUnavailableReason));
    }

    public static async Task<AdminSkillDiagnosticsEventsHttpResponse> GetEventsAsync(
        IClusterClient client,
        string? battleId,
        int? actorId,
        int? skillId,
        int limit)
    {
        var effectiveLimit = limit <= 0 ? 100 : Math.Min(limit, 500);
        var filters = new AdminSkillEventFilterHttpResponse(battleId, actorId, skillId, effectiveLimit);
        if (string.IsNullOrWhiteSpace(battleId))
        {
            return CreateUnavailableResponse(filters, "Battle id is required to query runtime diagnostics.");
        }

        try
        {
            var result = await client.GetGrain<IBattleLogicHostGrain>(battleId)
                .QueryDiagnosticEventsAsync(new BattleDiagnosticEventsQuery(
                    DateTime.UtcNow.Ticks,
                    actorId,
                    skillId,
                    effectiveLimit));
            return MapResult(result, battleId, filters);
        }
        catch (Exception exception)
        {
            return CreateUnavailableResponse(filters, $"Runtime diagnostic query failed: {exception.Message}");
        }
    }

    internal static AdminSkillDiagnosticsEventsHttpResponse MapResult(
        BattleDiagnosticEventsResult result,
        string battleId,
        AdminSkillEventFilterHttpResponse filters)
    {
        var events = (result.Events ?? Array.Empty<BattleDiagnosticEventRecord>())
            .Select(item => MapEvent(item, battleId, result))
            .ToArray();
        var isDataAvailable = string.Equals(
            result.Availability,
            "Available",
            StringComparison.Ordinal);
        var warnings = string.IsNullOrWhiteSpace(result.Message)
            ? Array.Empty<string>()
            : new[] { result.Message };

        return new AdminSkillDiagnosticsEventsHttpResponse(
            result.Status,
            filters,
            events,
            warnings,
            DateTime.UtcNow.Ticks,
            result.SchemaVersion,
            isDataAvailable,
            isDataAvailable ? null : result.Message,
            result.StoreRevision,
            result.HasMore,
            result.MonotonicTimestampFrequency,
            result.Offset,
            result.Limit,
            result.Availability);
    }

    internal static AdminSkillEventHttpResponse MapEvent(
        BattleDiagnosticEventRecord item,
        string battleId,
        BattleDiagnosticEventsResult result)
    {
        // Runtime fields are authoritative. Zero correlation IDs mean unknown, not an inferred edge.
        return new AdminSkillEventHttpResponse(
            item.Frame,
            checked((int)item.SourceActorId),
            item.SkillId,
            item.SkillInstanceId,
            item.EventType,
            item.EventType,
            item.TargetActorId == 0 ? null : checked((int)item.TargetActorId),
            null,
            item.Message,
            item.Outcome,
            battleId,
            result.WorldId,
            result.SessionId,
            item.Generation,
            item.Sequence,
            item.MonotonicTimestamp,
            item.NodeId,
            item.RootId,
            item.ParentId,
            item.SourceContextId,
            item.RootContextId,
            item.OwnerContextId);
    }

    private static AdminSkillDiagnosticsEventsHttpResponse CreateUnavailableResponse(
        AdminSkillEventFilterHttpResponse filters,
        string reason)
    {
        return new AdminSkillDiagnosticsEventsHttpResponse(
            RuntimeEventsUnavailable,
            filters,
            Array.Empty<AdminSkillEventHttpResponse>(),
            new[] { reason },
            DateTime.UtcNow.Ticks,
            RuntimeEventsSchemaVersion,
            false,
            reason,
            0,
            false,
            0,
            0,
            filters.Limit);
    }
}
