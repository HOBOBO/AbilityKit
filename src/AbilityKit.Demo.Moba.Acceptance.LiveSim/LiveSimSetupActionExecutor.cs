using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Protocol.Moba;
using AbilityKit.Scenario;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Acceptance.LiveSim;

/// <summary>
/// scenario setupActions 的 dotnet 执行器：五个动词（spawn_actor/set_attr/move_to/add_buff/remove_buff）+ wait/tick。
/// 忠实移植自 <c>MobaSkillConfigTestHarness</c> 与 <c>MobaAcceptanceSetupActionExecutor</c>，
/// 直接跑在 console 逻辑世界（<c>bootstrapper.RuntimeServices</c>）上。断言失败统一抛 <see cref="InvalidOperationException"/>（无测试框架依赖）。
/// </summary>
public sealed class LiveSimSetupActionExecutor : IAcceptanceObservationSource
{
    public const float DefaultFixedDelta = 1f / 30f;
    public const string DefaultLocalPlayerId = "player_1";

    private readonly ConsoleBattleBootstrapper _bootstrapper;
    private readonly Dictionary<string, int> _aliasToActorId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayerId> _aliasToPlayerId = new(StringComparer.Ordinal);

    public LiveSimSetupActionExecutor(ConsoleBattleBootstrapper bootstrapper)
        => _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));

    public float FixedDelta { get; set; } = DefaultFixedDelta;

    // —— 别名注册表 ——

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
        var services = Services;
        if (!services.TryResolve<MobaPlayerActorMapService>(out var map) || map == null)
            throw new InvalidOperationException("MobaPlayerActorMapService must be resolvable from the console world.");
        var resolvedPlayer = new PlayerId(playerId);
        if (!map.TryGetActorId(resolvedPlayer, out var actorId) || actorId <= 0)
            throw new InvalidOperationException($"Local player {playerId} must be bound to a runtime actor before seeding alias.");
        RegisterActorAlias(alias, actorId);
        _aliasToPlayerId[alias] = resolvedPlayer;
    }

    private IWorldResolver Services => _bootstrapper.RuntimeServices
        ?? throw new InvalidOperationException("RuntimeServices unavailable.");

    // —— 分发 ——

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
            default: throw new InvalidOperationException($"Unsupported setup action: {action.action}");
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

    // —— 五个动词 ——

    /// <summary>
    /// 生成一个场景 actor：优先按 heroId 解析完整 loadout（属性+技能）走 <c>TryInitializeFromLoadout</c>，
    /// 解析失败回退到「只给属性」的 <c>InitializeFromAttributeTemplate</c>（适合纯目标）。并把 PlayerId 绑进
    /// <c>MobaPlayerActorMapService</c>，使 <c>IMobaInputCoordinator</c> 能把技能输入路由给它。
    /// </summary>
    public int SpawnScenarioActor(
        string alias, int actorId, string kind, int teamId, int heroId, int attributeTemplateId,
        int level, int unitSubType, int mainType, string ownerPlayerId, int ownerActorId,
        string sourceKind, int sourceId, MobaAcceptanceVector3Expectation position)
    {
        var resolvedMainType = mainType != 0 ? (EntityMainType)mainType : EntityMainType.Unit;
        var resolvedUnitSubType = unitSubType != 0 ? (UnitSubType)unitSubType : UnitSubType.Hero;
        var entityKind = ResolveEntityKind(kind, resolvedMainType, resolvedUnitSubType);
        var ownerPlayer = new PlayerId(string.IsNullOrEmpty(ownerPlayerId) ? DefaultLocalPlayerId : ownerPlayerId);
        var services = Services;

        var loadout = ResolveLoadout(services, ownerPlayer, teamId, heroId, attributeTemplateId, level, position);

        MobaActorBuildSpec spec;
        if (loadout.HasValue)
        {
            var loadoutValue = loadout.Value;
            spec = MobaConverter.ToActorBuildSpec(actorId, in loadoutValue);
        }
        else
        {
            var transform = new Transform3(ToVec3(position), Quat.Identity, Vec3.One);
            var info = new MobaEntityInfo(
                actorId: actorId,
                kind: entityKind,
                transform: transform,
                team: (Team)(teamId > 0 ? teamId : 1),
                mainType: resolvedMainType,
                unitSubType: resolvedUnitSubType,
                ownerPlayer: ownerPlayer,
                templateId: heroId > 0 ? heroId : 1);
            spec = new MobaActorBuildSpec(in info, ResolveBuildSourceKind(sourceKind), sourceId, ownerActorId);
        }

        var request = MobaActorSpawnRequest.FromSpec(in spec);
        request.AllocateActorIdIfMissing = actorId <= 0;
        request.Initializer = (entity, _) =>
        {
            var init = services.Resolve<ActorEntityInitPipeline>();
            if (loadout.HasValue)
            {
                var value = loadout.Value;
                if (!init.TryInitializeFromLoadout(entity, in value, out var error))
                    throw new InvalidOperationException($"actor loadout initialization failed. alias={alias} heroId={heroId} error={error}");
            }
            else
            {
                init.InitializeFromAttributeTemplate(entity, attributeTemplateId > 0 ? attributeTemplateId : 1001);
            }
        };

        var spawn = services.Resolve<IMobaActorSpawnService>();
        if (!spawn.TrySpawn(in request, out var result))
            throw new InvalidOperationException($"Scenario spawn_actor failed. alias={alias} actorId={actorId} error={result.Error}");

        RegisterActorAlias(alias, result.ActorId);
        _aliasToPlayerId[alias] = ownerPlayer;
        services.Resolve<MobaPlayerActorMapService>().Bind(ownerPlayer, result.ActorId);
        return result.ActorId;
    }

    private static MobaPlayerLoadout? ResolveLoadout(
        IWorldResolver services, PlayerId ownerPlayer, int teamId, int heroId, int attributeTemplateId, int level,
        MobaAcceptanceVector3Expectation position)
    {
        if (heroId <= 0) return null;
        if (!services.TryResolve<MobaConfigDatabase>(out var config) || config == null) return null;

        var spawnPosition = ToVec3(position);
        if (MobaHeroLoadoutResolver.TryResolve(config, ownerPlayer, teamId, heroId,
                level > 0 ? level : 1, 0, in spawnPosition, out var loadout, out _))
        {
            return loadout;
        }
        return null;
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

        if (string.IsNullOrEmpty(property)) throw new InvalidOperationException("set_attr requires property.");
        var normalized = property.Replace(".", "_").Replace("-", "_");
        if (!Enum.TryParse(normalized, ignoreCase: true, out BattleAttributeType type) || type == BattleAttributeType.None)
            throw new InvalidOperationException($"Unsupported set_attr property: {property}");
        attrs.SetBase(type, value);
    }

    public void AddScenarioBuff(int targetActorId, int buffId, int sourceActorId, int durationOverrideMs)
    {
        if (targetActorId <= 0) throw new InvalidOperationException("add_buff requires target actor.");
        if (buffId <= 0) throw new InvalidOperationException("add_buff requires buffId.");
        var buffs = Services.Resolve<MobaBuffService>();
        if (!buffs.ApplyBuffImmediate(targetActorId, buffId, sourceActorId, durationOverrideMs))
            throw new InvalidOperationException($"add_buff failed. targetActorId={targetActorId} buffId={buffId} sourceActorId={sourceActorId}");
    }

    public void RemoveScenarioBuff(int targetActorId, int buffId, int sourceActorId, bool removeAll)
    {
        if (targetActorId <= 0) throw new InvalidOperationException("remove_buff requires target actor.");
        if (buffId <= 0) throw new InvalidOperationException("remove_buff requires buffId.");
        var buffs = Services.Resolve<MobaBuffService>();
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

    public int GetActorTeamId(int actorId)
    {
        var entity = AssertActorEntity(actorId);
        return entity.hasTeam ? (int)entity.team.Value : 0;
    }

    public float GetActorMana(int actorId) => new MobaAttrs(AssertActorEntity(actorId)).Mana;
    public float GetActorMaxHp(int actorId) => new MobaAttrs(AssertActorEntity(actorId)).MaxHp;
    public float GetActorMaxMana(int actorId) => new MobaAttrs(AssertActorEntity(actorId)).MaxMana;

    public int CountActorBuffs(int actorId, int buffId = 0)
    {
        var entity = AssertActorEntity(actorId);
        if (!entity.hasBuffs || entity.buffs.Active == null) return 0;
        var count = 0;
        for (var i = 0; i < entity.buffs.Active.Count; i++)
        {
            var runtime = entity.buffs.Active[i];
            if (runtime != null && (buffId <= 0 || runtime.BuffId == buffId)) count++;
        }
        return count;
    }

    public AcceptanceObservations CaptureObservations(MobaAcceptanceExpectation expectation)
    {
        var states = PickStateExpectations(expectation);
        var values = new List<AcceptanceObservation>(states.Length);
        foreach (var state in states)
        {
            if (state == null) continue;
            var alias = state.alias ?? string.Empty;
            if (!TryGetActorId(alias, out var actorId)) continue;
            var property = (state.property ?? string.Empty).Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty);
            object value;
            switch (property)
            {
                case "exists":
                case "present":
                case "bound": value = true; break;
                case "actorid":
                case "id": value = actorId; break;
                case "hp": value = GetActorHp(actorId); break;
                case "mana": value = GetActorMana(actorId); break;
                case "maxhp": value = GetActorMaxHp(actorId); break;
                case "maxmana": value = GetActorMaxMana(actorId); break;
                case "teamid": value = GetActorTeamId(actorId); break;
                case "buff":
                case "hasbuff": value = CountActorBuffs(actorId, state.expectedInt) > 0; break;
                case "buffcount": value = CountActorBuffs(actorId, state.expectedInt); break;
                case "position":
                case "transform.position":
                {
                    var position = GetActorPosition(actorId);
                    value = new TestVector3(position.X, position.Y, position.Z);
                    break;
                }
                default: continue;
            }
            values.Add(new AcceptanceObservation(alias, actorId.ToString(), null, state.property ?? string.Empty, value));
        }
        return new AcceptanceObservations { States = values };
    }

    AcceptanceObservations IAcceptanceObservationSource.Capture(TestScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var expectations = scenario.Expectations as TestExpectations;
        return CaptureObservations(new MobaAcceptanceExpectation
        {
            scenario = new MobaAcceptanceScenarioExpectation
            {
                stateExpectations = expectations?.State ?? Array.Empty<MobaAcceptanceStateExpectation>(),
            },
        });
    }

    private static MobaAcceptanceStateExpectation[] PickStateExpectations(MobaAcceptanceExpectation expectation)
        => expectation.scenario?.stateExpectations is { Length: > 0 } preferred
            ? preferred
            : expectation.stateExpectations ?? Array.Empty<MobaAcceptanceStateExpectation>();

    // —— tick ——

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
        if (!TryGetActorEntity(actorId, out var entity))
            throw new InvalidOperationException($"Actor entity missing: {actorId}");
        return entity;
    }

    private bool TryGetActorEntity(int actorId, out ActorEntity entity)
    {
        entity = null;
        if (actorId <= 0) return false;
        var lookup = Services.Resolve<MobaActorLookupService>();
        return lookup.TryGetActorEntity(actorId, out entity) && entity != null;
    }

    private int ResolveRequiredActorId(string alias, string actorIdText, int explicitActorId, string command)
    {
        var actorId = ResolveOptionalActorId(alias, explicitActorId);
        if (actorId <= 0 && int.TryParse(actorIdText, out var parsed) && parsed > 0) actorId = parsed;
        if (actorId <= 0) throw new InvalidOperationException(command + " requires actorAlias, actorId or targetActorId.");
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

    // —— Execute 分发的私有实现 ——

    private void ExecuteSpawnActor(MobaAcceptanceSetupActionExpectation action)
    {
        var alias = FirstNonEmpty(action.alias, action.actorAlias, action.targetAlias);
        if (string.IsNullOrEmpty(alias)) throw new InvalidOperationException("spawn_actor requires alias or actorAlias.");
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
        if (action.position == null) throw new InvalidOperationException("move_to requires position.");
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
