using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 切片 2：scenario setupActions 的 dotnet 移植。
/// 五个动词（spawn_actor/set_attr/move_to/add_buff/remove_buff）+ wait/tick 忠实移植自
/// <c>MobaSkillConfigTestHarness</c>（512-611 行）与 <c>MobaAcceptanceSetupActionExecutor</c>，
/// 但直接跑在 console 逻辑世界（<c>bootstrapper.RuntimeServices</c>）上——所有服务都是可 Resolve 的
/// <c>[WorldService]</c>，与 Unity harness 完全无关。NUnit <c>Assert</c> 换成 xUnit。
/// </summary>
public sealed class LiveSimSetupActionExecutor
{
    public const float DefaultFixedDelta = 1f / 30f;
    public const string DefaultLocalPlayerId = "player_1";

    private readonly ConsoleBattleBootstrapper _bootstrapper;
    private readonly Dictionary<string, int> _aliasToActorId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayerId> _aliasToPlayerId = new(StringComparer.Ordinal);

    public LiveSimSetupActionExecutor(ConsoleBattleBootstrapper bootstrapper)
        => _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));

    public float FixedDelta { get; set; } = DefaultFixedDelta;

    // —— 别名注册表（移植 harness alias map）——

    public bool TryGetActorId(string alias, out int actorId)
    {
        actorId = 0;
        return !string.IsNullOrEmpty(alias) && _aliasToActorId.TryGetValue(alias, out actorId) && actorId > 0;
    }

    public void RegisterActorAlias(string alias, int actorId)
    {
        if (!string.IsNullOrEmpty(alias) && actorId > 0) _aliasToActorId[alias] = actorId;
    }

    public bool TryGetPlayerId(string alias, out PlayerId playerId)
    {
        playerId = default;
        return !string.IsNullOrEmpty(alias) && _aliasToPlayerId.TryGetValue(alias, out playerId);
    }

    /// <summary>把本地玩家（console 默认 player_1）的 actor 注册为别名——acceptance 场景的 caster 通常绑定本地玩家。</summary>
    public void SeedLocalPlayerAlias(string alias, string playerId = DefaultLocalPlayerId)
    {
        var services = _bootstrapper.RuntimeServices;
        Assert.NotNull(services);
        Assert.True(services!.TryResolve<MobaPlayerActorMapService>(out var map) && map != null,
            "MobaPlayerActorMapService must be resolvable from the console world.");
        var resolvedPlayer = new PlayerId(playerId);
        Assert.True(map!.TryGetActorId(resolvedPlayer, out var actorId) && actorId > 0,
            $"Local player {playerId} must be bound to a runtime actor before seeding alias.");
        RegisterActorAlias(alias, actorId);
        _aliasToPlayerId[alias] = resolvedPlayer;
    }

    // —— 分发（镜像 MobaAcceptanceSetupActionExecutor.Execute 的语义）——

    public void Execute(MobaAcceptanceSetupActionExpectation action)
    {
        if (action == null) return;

        if (IsWaitAction(action.action))
        {
            TickMilliseconds(action.durationMs);
            return;
        }

        switch (Normalize(action.action))
        {
            case "spawn_actor": ExecuteSpawnActor(action); return;
            case "set_attr": ExecuteSetAttr(action); return;
            case "move_to": ExecuteMoveTo(action); return;
            case "add_buff": ExecuteAddBuff(action); return;
            case "remove_buff": ExecuteRemoveBuff(action); return;
            default: Assert.Fail($"Unsupported setup action: {action.action}"); return;
        }
    }

    public static bool IsWaitAction(string action)
        => string.Equals(action, "wait", StringComparison.OrdinalIgnoreCase)
           || string.Equals(action, "tick", StringComparison.OrdinalIgnoreCase);

    public static bool IsEnvironmentCommand(string action)
    {
        var command = Normalize(action);
        return string.Equals(command, "spawn_actor", StringComparison.Ordinal)
               || string.Equals(command, "set_attr", StringComparison.Ordinal)
               || string.Equals(command, "move_to", StringComparison.Ordinal)
               || string.Equals(command, "add_buff", StringComparison.Ordinal)
               || string.Equals(command, "remove_buff", StringComparison.Ordinal);
    }

    // —— 五个动词：忠实移植 harness 512-611（World.Services → bootstrapper.RuntimeServices）——

    public int SpawnScenarioActor(
        string alias, int actorId, string kind, int teamId, int heroId, int attributeTemplateId,
        int level, int unitSubType, int mainType, string ownerPlayerId, int ownerActorId,
        string sourceKind, int sourceId, MobaAcceptanceVector3Expectation position)
    {
        var resolvedMainType = mainType != 0 ? (EntityMainType)mainType : EntityMainType.Unit;
        var resolvedUnitSubType = unitSubType != 0 ? (UnitSubType)unitSubType : UnitSubType.Hero;
        var entityKind = ResolveEntityKind(kind, resolvedMainType, resolvedUnitSubType);
        var transform = new Transform3(ToVec3(position), Quat.Identity, Vec3.One);
        var ownerPlayer = new PlayerId(string.IsNullOrEmpty(ownerPlayerId) ? DefaultLocalPlayerId : ownerPlayerId);
        var info = new MobaEntityInfo(
            actorId: actorId,
            kind: entityKind,
            transform: transform,
            team: (Team)(teamId > 0 ? teamId : 1),
            mainType: resolvedMainType,
            unitSubType: resolvedUnitSubType,
            ownerPlayer: ownerPlayer,
            templateId: heroId > 0 ? heroId : 1);
        var spec = new MobaActorBuildSpec(
            in info,
            ResolveBuildSourceKind(sourceKind),
            sourceId,
            ownerActorId);
        var request = MobaActorSpawnRequest.FromSpec(in spec);
        request.AllocateActorIdIfMissing = actorId <= 0;
        request.Initializer = (entity, _) =>
            _bootstrapper.RuntimeServices.Resolve<ActorEntityInitPipeline>()
                .InitializeFromAttributeTemplate(entity, attributeTemplateId > 0 ? attributeTemplateId : 1001);

        var services = _bootstrapper.RuntimeServices;
        var spawn = services.Resolve<IMobaActorSpawnService>();
        Assert.True(spawn.TrySpawn(in request, out var result),
            $"Scenario spawn_actor failed. alias={alias} actorId={actorId} error={result.Error}");
        RegisterActorAlias(alias, result.ActorId);
        _aliasToPlayerId[alias] = ownerPlayer;
        return result.ActorId;
    }

    public void MoveScenarioActor(int actorId, MobaAcceptanceVector3Expectation position)
    {
        var entity = AssertActorEntity(actorId);
        var current = entity.hasTransform ? entity.transform.Value : Transform3.Identity;
        entity.ReplaceTransform(new Transform3(ToVec3(position), current.Rotation, current.Scale));
    }

    public void SetScenarioActorAttribute(int actorId, string property, float value)
    {
        var entity = AssertActorEntity(actorId);
        var attrs = new MobaAttrs(entity);
        if (string.Equals(property, "hp", StringComparison.OrdinalIgnoreCase)) { attrs.Hp = value; return; }
        if (string.Equals(property, "mana", StringComparison.OrdinalIgnoreCase)) { attrs.Mana = value; return; }
        if (string.Equals(property, "rage", StringComparison.OrdinalIgnoreCase)) { attrs.Rage = value; return; }

        Assert.False(string.IsNullOrEmpty(property), "set_attr requires property.");
        var normalized = property.Replace(".", "_").Replace("-", "_");
        Assert.True(Enum.TryParse(normalized, ignoreCase: true, out BattleAttributeType type) && type != BattleAttributeType.None,
            $"Unsupported set_attr property: {property}");
        attrs.SetBase(type, value);
    }

    public void AddScenarioBuff(int targetActorId, int buffId, int sourceActorId, int durationOverrideMs)
    {
        Assert.True(targetActorId > 0, "add_buff requires target actor.");
        Assert.True(buffId > 0, "add_buff requires buffId.");
        var buffs = _bootstrapper.RuntimeServices.Resolve<MobaBuffService>();
        Assert.True(buffs.ApplyBuffImmediate(targetActorId, buffId, sourceActorId, durationOverrideMs),
            $"add_buff failed. targetActorId={targetActorId} buffId={buffId} sourceActorId={sourceActorId}");
    }

    public void RemoveScenarioBuff(int targetActorId, int buffId, int sourceActorId, bool removeAll)
    {
        Assert.True(targetActorId > 0, "remove_buff requires target actor.");
        Assert.True(buffId > 0, "remove_buff requires buffId.");
        var buffs = _bootstrapper.RuntimeServices.Resolve<MobaBuffService>();
        if (removeAll)
        {
            buffs.RemoveBuffsImmediate(targetActorId, buffId, sourceActorId, removeAll: true, TraceLifecycleReason.Dispelled);
            return;
        }
        buffs.RemoveBuffImmediate(targetActorId, buffId, sourceActorId, TraceLifecycleReason.Dispelled);
    }

    // —— 状态读取（供断言）——

    public float GetActorHp(int actorId) => new MobaAttrs(AssertActorEntity(actorId)).Hp;

    public Vec3 GetActorPosition(int actorId)
    {
        var entity = AssertActorEntity(actorId);
        return entity.hasTransform ? entity.transform.Value.Position : Vec3.Zero;
    }

    /// <summary>诊断用：actor 是否携带 Collider 组件（碰撞体同步进碰撞世界的前提）。</summary>
    public bool HasCollider(int actorId) => AssertActorEntity(actorId).hasCollider;

    /// <summary>诊断用：actor 是否已注册进碰撞世界（CollisionWorldSyncSystem 每帧把 Transform+Collider 实体注册并赋 CollisionId）。</summary>
    public bool HasCollisionId(int actorId) => AssertActorEntity(actorId).hasCollisionId;

    // —— tick ——

    /// <summary>
    /// 移除 actor 的 AI brain（ActorBrain 组件）。acceptance 场景中玩家驱动的 actor
    /// 不应被 brain/PathFollowing 带偏（console 本地玩家英雄默认带 brain；Unity harness 的 loadout 英雄不带）。
    /// </summary>
    public void DisableActorBrain(int actorId)
    {
        var entity = AssertActorEntity(actorId);
        if (entity.hasActorBrain) entity.RemoveActorBrain();
    }

    public void Tick(int ticks)
    {
        for (var i = 0; i < ticks; i++) _bootstrapper.Tick();
    }

    public void TickMilliseconds(int milliseconds)
    {
        if (milliseconds <= 0) return;
        Tick(Math.Max(1, (int)Math.Round(milliseconds / (FixedDelta * 1000f))));
    }

    // —— helpers ——

    private static string Normalize(string action) => string.IsNullOrEmpty(action) ? string.Empty : action.Trim().ToLowerInvariant();

    private static Vec3 ToVec3(MobaAcceptanceVector3Expectation value)
        => value == null ? Vec3.Zero : new Vec3(value.x, value.y, value.z);

    private static MobaEntityKind ResolveEntityKind(string kind, EntityMainType mainType, UnitSubType unitSubType)
    {
        if (!string.IsNullOrEmpty(kind) && Enum.TryParse(kind, ignoreCase: true, out MobaEntityKind parsed) && parsed != MobaEntityKind.Unknown)
        {
            return parsed;
        }
        return ActorArchetypeFactory.CreateKindFromType(mainType, unitSubType);
    }

    private static MobaActorBuildSourceKind ResolveBuildSourceKind(string sourceKind)
    {
        if (!string.IsNullOrEmpty(sourceKind) && Enum.TryParse(sourceKind, ignoreCase: true, out MobaActorBuildSourceKind parsed))
        {
            return parsed;
        }
        return MobaActorBuildSourceKind.Unknown;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return null;
        for (var i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) return values[i];
        return null;
    }

    private ActorEntity AssertActorEntity(int actorId)
    {
        Assert.True(TryGetActorEntity(actorId, out var entity), $"Actor entity missing: {actorId}");
        return entity;
    }

    private bool TryGetActorEntity(int actorId, out ActorEntity entity)
    {
        entity = null;
        if (actorId <= 0) return false;
        var lookup = _bootstrapper.RuntimeServices.Resolve<MobaActorLookupService>();
        return lookup.TryGetActorEntity(actorId, out entity) && entity != null;
    }

    private int ResolveRequiredActorId(string alias, string actorIdText, int explicitActorId, string command)
    {
        var actorId = ResolveOptionalActorId(alias, explicitActorId);
        if (actorId <= 0 && int.TryParse(actorIdText, out var parsed) && parsed > 0) actorId = parsed;
        Assert.True(actorId > 0, command + " requires actorAlias, actorId or targetActorId.");
        return actorId;
    }

    private int ResolveOptionalActorId(string alias, int explicitActorId)
    {
        if (explicitActorId > 0) return explicitActorId;
        if (TryGetActorId(alias, out var actorId)) return actorId;
        return 0;
    }

    private static float ResolveValue(MobaAcceptanceSetupActionExpectation action)
    {
        if (Math.Abs(action.value) > float.Epsilon) return action.value;
        if (action.intValue != 0) return action.intValue;
        return 0f;
    }

    // —— Execute 分发的私有实现（镜像 MobaAcceptanceSetupActionExecutor）——

    private void ExecuteSpawnActor(MobaAcceptanceSetupActionExpectation action)
    {
        var alias = FirstNonEmpty(action.alias, action.actorAlias, action.targetAlias);
        Assert.False(string.IsNullOrEmpty(alias), "spawn_actor requires alias or actorAlias.");
        var parsedActorId = action.actorId != null && int.TryParse(action.actorId, out var p) && p > 0 ? p : 0;
        var ownerActorId = action.ownerActorId > 0 ? action.ownerActorId : ResolveOptionalActorId(action.sourceAlias, action.sourceActorId);
        SpawnScenarioActor(
            alias,
            parsedActorId,
            action.kind, action.teamId, action.heroId, action.attributeTemplateId, action.level,
            action.unitSubType, action.mainType, action.playerId, ownerActorId,
            action.sourceKind, action.sourceId, action.position);
    }

    private void ExecuteSetAttr(MobaAcceptanceSetupActionExpectation action)
    {
        var actorId = ResolveRequiredActorId(action.actorAlias, action.actorId, action.targetActorId, "set_attr");
        SetScenarioActorAttribute(actorId, action.property, ResolveValue(action));
    }

    private void ExecuteMoveTo(MobaAcceptanceSetupActionExpectation action)
    {
        var actorId = ResolveRequiredActorId(action.actorAlias, action.actorId, action.targetActorId, "move_to");
        Assert.NotNull(action.position);
        MoveScenarioActor(actorId, action.position);
    }

    private void ExecuteAddBuff(MobaAcceptanceSetupActionExpectation action)
    {
        var targetActorId = ResolveRequiredActorId(FirstNonEmpty(action.targetAlias, action.actorAlias), action.actorId, action.targetActorId, "add_buff");
        var sourceActorId = ResolveOptionalActorId(action.sourceAlias, action.sourceActorId);
        if (sourceActorId <= 0) sourceActorId = targetActorId;
        AddScenarioBuff(targetActorId, action.buffId, sourceActorId, action.durationOverrideMs);
    }

    private void ExecuteRemoveBuff(MobaAcceptanceSetupActionExpectation action)
    {
        var targetActorId = ResolveRequiredActorId(FirstNonEmpty(action.targetAlias, action.actorAlias), action.actorId, action.targetActorId, "remove_buff");
        var sourceActorId = ResolveOptionalActorId(action.sourceAlias, action.sourceActorId);
        RemoveScenarioBuff(targetActorId, action.buffId, sourceActorId, action.removeAll);
    }
}
