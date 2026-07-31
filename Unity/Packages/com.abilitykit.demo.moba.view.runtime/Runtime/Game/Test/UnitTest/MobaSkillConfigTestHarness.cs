using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Collision;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Protocol.Moba;
using AbilityKit.Trace;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Reusable EditMode fixture for config-driven MOBA skill tests.
    /// It owns a headless moba.view runtime world and exposes common config, input and trace assertions
    /// so new skill, condition and trigger-action tests can focus on ids and expected behavior.
    /// </summary>
    public sealed class MobaSkillConfigTestHarness : IDisposable
    {
        public const float DefaultFixedDelta = 1f / 30f;
        public const string DefaultWorldType = "battle";
        public const string DefaultPlayerId = "p1";

        private bool _disposed;
        private readonly Dictionary<string, int> _aliasToActorId = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, PlayerId> _aliasToPlayerId = new Dictionary<string, PlayerId>(StringComparer.Ordinal);
        private readonly MobaAcceptanceScenarioExpectation _scenario;
        private readonly MobaAcceptanceActorExpectation[] _scenarioActors;
        private readonly MobaGameStartSpec _gameStartSpec;

        private MobaSkillConfigTestHarness(IWorld world, PlayerId playerId, float fixedDelta, int tickRate, MobaGameStartSpec gameStartSpec, MobaAcceptanceScenarioExpectation scenario = null, MobaAcceptanceActorExpectation[] scenarioActors = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            PlayerId = playerId;
            FixedDelta = fixedDelta;
            TickRate = tickRate;
            _gameStartSpec = gameStartSpec;
            _scenario = scenario;
            _scenarioActors = scenarioActors ?? Array.Empty<MobaAcceptanceActorExpectation>();
            _aliasToPlayerId[playerId.Value] = playerId;
            CacheScenarioAliases(_scenarioActors);
        }

        public IWorld World { get; }
        public PlayerId PlayerId { get; }
        public float FixedDelta { get; }
        public int TickRate { get; }
        public MobaAcceptanceScenarioExpectation Scenario => _scenario;
        public MobaAcceptanceActorExpectation[] ScenarioActors => _scenarioActors;
        public IReadOnlyDictionary<string, int> ActorAliases => _aliasToActorId;
        public IReadOnlyDictionary<string, PlayerId> PlayerAliases => _aliasToPlayerId;

        public MobaConfigDatabase Config => World.Services.Resolve<MobaConfigDatabase>();
        public MobaTraceRegistry Trace => World.Services.Resolve<MobaTraceRegistry>();
        public TriggerPlanJsonDatabase TriggerPlans => World.Services.Resolve<TriggerPlanJsonDatabase>();
        public IFrameTime FrameTime => World.Services.Resolve<IFrameTime>();

        public static MobaSkillConfigTestHarness CreateForSinglePlayer(
            int[] skillIds,
            string worldId = "skill_config_test_world",
            string worldType = DefaultWorldType,
            string playerId = DefaultPlayerId,
            int attributeTemplateId = 1001,
            int heroId = 1,
            int basicAttackSkillId = 0,
            int tickRate = 30,
            int inputDelayFrames = 0,
            float fixedDelta = DefaultFixedDelta,
            MobaRuntimeValidationMode validationMode = MobaRuntimeValidationMode.ManualOnly)
        {
            if (skillIds == null) throw new ArgumentNullException(nameof(skillIds));

            var typedPlayerId = new PlayerId(playerId);
            var launchSpec = BuildLaunchSpec(
                worldId: new WorldId(worldId),
                worldType: worldType,
                playerId: typedPlayerId,
                players: new[]
                {
                    new MobaPlayerLoadout(
                        typedPlayerId,
                        teamId: 1,
                        heroId: heroId,
                        attributeTemplateId: attributeTemplateId,
                        level: 1,
                        basicAttackSkillId: basicAttackSkillId > 0
                            ? basicAttackSkillId
                            : ResolveBasicAttackSkillId(heroId, attributeTemplateId),
                        skillIds: skillIds,
                        spawnIndex: 0,
                        unitSubType: (int)UnitSubType.Hero,
                        mainType: (int)EntityMainType.Unit,
                        hasSpawnPosition: 1,
                        spawnX: 0f,
                        spawnY: 0f,
                        spawnZ: 0f)
                },
                tickRate: tickRate,
                inputDelayFrames: inputDelayFrames);
            var world = CreateHeadlessMobaWorld(new WorldId(worldId), worldType, in launchSpec, validationMode);

            return new MobaSkillConfigTestHarness(world, typedPlayerId, fixedDelta, tickRate, launchSpec.ToGameStartSpec());
        }

        public static MobaSkillConfigTestHarness CreateForScenario(
            MobaAcceptanceExpectation expectation,
            string worldId = null,
            string worldType = DefaultWorldType,
            string playerId = null,
            int tickRate = 0,
            int inputDelayFrames = 0,
            float fixedDelta = DefaultFixedDelta)
        {
            if (expectation == null) throw new ArgumentNullException(nameof(expectation));

            var scenario = expectation.scenario;
            var scenarioActors = GetScenarioActors(expectation);
            var resolvedWorldId = !string.IsNullOrEmpty(worldId)
                ? worldId
                : !string.IsNullOrEmpty(scenario?.worldId)
                    ? scenario.worldId
                    : !string.IsNullOrEmpty(expectation.worldId)
                        ? expectation.worldId
                        : (string.IsNullOrEmpty(expectation.caseId) ? "moba_acceptance_world" : expectation.caseId + "_world");
            var resolvedTickRate = tickRate > 0
                ? tickRate
                : scenario != null && scenario.tickRate > 0
                    ? scenario.tickRate
                    : expectation.tickRate > 0
                        ? expectation.tickRate
                        : 30;
            var resolvedPlayerId = !string.IsNullOrEmpty(playerId)
                ? playerId
                : ResolvePrimaryPlayerId(expectation, scenarioActors);
            var typedPlayerId = new PlayerId(resolvedPlayerId);
            var players = BuildPlayerLoadouts(expectation, scenarioActors, typedPlayerId, inputDelayFrames);
            var launchSpec = BuildLaunchSpec(
                worldId: new WorldId(resolvedWorldId),
                worldType: worldType,
                playerId: typedPlayerId,
                players: players,
                tickRate: resolvedTickRate,
                inputDelayFrames: inputDelayFrames);
            var world = CreateHeadlessMobaWorld(new WorldId(resolvedWorldId), worldType, in launchSpec);

            return new MobaSkillConfigTestHarness(world, typedPlayerId, fixedDelta, resolvedTickRate, launchSpec.ToGameStartSpec(), scenario, scenarioActors);
        }

        public void EnterGameAndWarmup(int warmupTicks = 3, string reason = "editmode skill config test")
        {
            var phase = World.Services.Resolve<MobaLogicWorldRunGateService>();
            if (!phase.InGame)
            {
                var startPort = World.Services.Resolve<IMobaGameStartPort>();
                var startResult = startPort.TryStartGame(in _gameStartSpec);
                Assert.IsTrue(startResult.Succeeded, $"Formal game start failed: {startResult}");
                phase.SetInGame(reason);
            }

            Tick(warmupTicks);
            RefreshScenarioActorAliases();
            RepairScenarioPlayerActorBindings();
            RefreshScenarioActorAliases();
        }

        public bool TryGetActorId(string alias, out int actorId)
        {
            if (string.IsNullOrEmpty(alias))
            {
                actorId = 0;
                return false;
            }

            return _aliasToActorId.TryGetValue(alias, out actorId) && actorId > 0;
        }

        public bool TryGetPlayerId(string alias, out PlayerId playerId)
        {
            if (string.IsNullOrEmpty(alias))
            {
                playerId = default;
                return false;
            }

            return _aliasToPlayerId.TryGetValue(alias, out playerId) && !string.IsNullOrEmpty(playerId.Value);
        }

        public int AssertActorId(string alias, string message = null)
        {
            Assert.IsTrue(TryGetActorId(alias, out var actorId), message ?? $"Actor alias missing: {alias}");
            return actorId;
        }

        public void RegisterActorAlias(string alias, int actorId)
        {
            if (string.IsNullOrEmpty(alias) || actorId <= 0) return;
            _aliasToActorId[alias] = actorId;
        }

        public bool TryGetActorEntity(int actorId, out ActorEntity entity)
        {
            entity = null;
            if (actorId <= 0) return false;
            var lookup = World.Services.Resolve<MobaActorLookupService>();
            return lookup.TryGetActorEntity(actorId, out entity) && entity != null;
        }

        public ActorEntity AssertActorEntity(int actorId, string message = null)
        {
            Assert.IsTrue(TryGetActorEntity(actorId, out var entity), message ?? $"Actor entity missing: {actorId}");
            return entity;
        }

        public ActorEntity AssertActorEntity(string alias, string message = null)
        {
            var actorId = AssertActorId(alias, message ?? $"Actor alias missing: {alias}");
            return AssertActorEntity(actorId, message ?? $"Actor entity missing for alias {alias}({actorId}).");
        }

        public float GetActorHp(int actorId, string message = null)
        {
            var entity = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            var attrs = new MobaAttrs(entity);
            return attrs.Hp;
        }

        public float GetActorHp(string alias, string message = null)
        {
            var actorId = AssertActorId(alias, message ?? $"Actor alias missing: {alias}");
            return GetActorHp(actorId, message ?? $"Actor entity missing for alias {alias}({actorId}).");
        }

        public float GetActorMoveSpeed(int actorId, string message = null)
        {
            var entity = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            var attrs = new MobaAttrs(entity);
            return attrs.MoveSpeed;
        }

        public float GetActorMoveSpeed(string alias, string message = null)
        {
            var actorId = AssertActorId(alias, message ?? $"Actor alias missing: {alias}");
            return GetActorMoveSpeed(actorId, message ?? $"Actor entity missing for alias {alias}({actorId}).");
        }

        public float GetActorRage(int actorId, string message = null)
        {
            var entity = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            var attrs = new MobaAttrs(entity);
            return attrs.Rage;
        }

        public float GetActorRage(string alias, string message = null)
        {
            var actorId = AssertActorId(alias, message ?? $"Actor alias missing: {alias}");
            return GetActorRage(actorId, message ?? $"Actor entity missing for alias {alias}({actorId}).");
        }

        public float GetActorAttribute(int actorId, BattleAttributeType type, string message = null)
        {
            var entity = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            var attrs = new MobaAttrs(entity);
            return attrs.Get(type);
        }

        public float GetActorAttribute(string alias, BattleAttributeType type, string message = null)
        {
            var actorId = AssertActorId(alias, message ?? $"Actor alias missing: {alias}");
            return GetActorAttribute(actorId, type, message ?? $"Actor entity missing for alias {alias}({actorId}).");
        }

        public bool HasActorBuff(int actorId, int buffId, string message = null)
        {
            Assert.Greater(buffId, 0, message ?? "buffId must be positive.");
            var entity = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            if (!entity.hasBuffs || entity.buffs.Active == null) return false;

            for (var i = 0; i < entity.buffs.Active.Count; i++)
            {
                var runtime = entity.buffs.Active[i];
                if (runtime == null) continue;
                if (runtime.BuffId == buffId) return true;
            }

            return false;
        }

        public bool HasActorBuff(string alias, int buffId, string message = null)
        {
            var actorId = AssertActorId(alias, message ?? $"Actor alias missing: {alias}");
            return HasActorBuff(actorId, buffId, message ?? $"Actor entity missing for alias {alias}({actorId}).");
        }

        public bool TryGetActorBuffStackCount(int actorId, int buffId, out int stackCount, string message = null)
        {
            stackCount = 0;
            Assert.Greater(buffId, 0, message ?? "buffId must be positive.");
            var entity = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            if (!entity.hasBuffs || entity.buffs.Active == null) return false;

            for (var i = 0; i < entity.buffs.Active.Count; i++)
            {
                var runtime = entity.buffs.Active[i];
                if (runtime == null || runtime.BuffId != buffId) continue;

                stackCount = runtime.StackCount;
                return true;
            }

            return false;
        }

        public bool TryGetActorBuffRemainingSeconds(int actorId, int buffId, out float remainingSeconds, string message = null)
        {
            remainingSeconds = 0f;
            Assert.Greater(buffId, 0, message ?? "buffId must be positive.");
            var entity = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            if (!entity.hasBuffs || entity.buffs.Active == null) return false;

            for (var i = 0; i < entity.buffs.Active.Count; i++)
            {
                var runtime = entity.buffs.Active[i];
                if (runtime == null || runtime.BuffId != buffId) continue;

                remainingSeconds = runtime.Continuous != null ? runtime.Continuous.RemainingSeconds : runtime.Remaining;
                return true;
            }

            return false;
        }

        public float AssertActorBuffRemainingSeconds(int actorId, int buffId, string message = null)
        {
            Assert.IsTrue(TryGetActorBuffRemainingSeconds(actorId, buffId, out var remainingSeconds, message), message ?? $"Actor {actorId} should have buff {buffId}.");
            return remainingSeconds;
        }

        public bool TryGetRunningSkillSnapshot(int actorId, int slot, out SkillPipelineRunner.RunningSnapshot snapshot)
        {
            snapshot = default;
            if (actorId <= 0 || slot <= 0) return false;
            if (!World.Services.TryResolve<SkillCastCoordinator>(out var skills) || skills == null) return false;
            return skills.TryGetRunningBySlot(actorId, slot, out snapshot);
        }

        public void TickUntilSkillStops(int actorId, int slot, int maxTicks, string message = null)
        {
            Assert.Greater(maxTicks, 0, message ?? "maxTicks must be positive.");
            for (var i = 0; i < maxTicks; i++)
            {
                if (!TryGetRunningSkillSnapshot(actorId, slot, out _)) return;
                Tick(1);
            }

            Assert.Fail((message ?? "Skill pipeline did not stop within the expected test window.") + " " + DescribeSkillRuntimeState(actorId, slot));
        }

        public string DescribeSkillRuntimeState(int actorId, int slot)
        {
            var frame = FrameTime;
            World.Services.TryResolve<IWorldClock>(out var clock);
            var traceCount = 0;
            foreach (MobaTraceKind kind in Enum.GetValues(typeof(MobaTraceKind)))
            {
                if (kind == MobaTraceKind.None) continue;
                foreach (var node in Trace.GetNodesByKind((int)kind))
                {
                    if (node.IsValid) traceCount++;
                }
            }

            var timeSummary = $"frame={frame.Frame.Value}, timeMs={(int)Math.Round(frame.Time * 1000d)}, deltaMs={(int)Math.Round(frame.DeltaTime * 1000d)}, clockTimeMs={(int)Math.Round((clock != null ? clock.Time : 0f) * 1000d)}, clockDeltaMs={(int)Math.Round((clock != null ? clock.DeltaTime : 0f) * 1000d)}";
            var worldSummary = DescribeWorldComposition();
            if (!TryGetRunningSkillSnapshot(actorId, slot, out var snapshot))
            {
                return $"actor={actorId}, slot={slot}, {timeSummary}, running=false, traceNodes={traceCount}, world={worldSummary}";
            }

            var runtimeSummary = DescribeRunnerRuntimeState(actorId, slot);
            return $"actor={actorId}, slot={slot}, {timeSummary}, running=true, skillId={snapshot.SkillId}, stage={snapshot.Stage}, elapsedMs={snapshot.ElapsedMs}, nextEventIndex={snapshot.NextEventIndex}, targetActorId={snapshot.TargetActorId}, traceNodes={traceCount}, runner={runtimeSummary}, world={worldSummary}";
        }

        private string DescribeRunnerRuntimeState(int actorId, int slot)
        {
            if (actorId <= 0 || slot <= 0) return "invalid";
            if (!World.Services.TryResolve<SkillCastCoordinator>(out var coordinator) || coordinator == null) return "coordinatorMissing";

            var coordinatorType = typeof(SkillCastCoordinator);
            var registryField = coordinatorType.GetField("_runnerRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
            var registry = registryField?.GetValue(coordinator);
            if (registry == null) return "registryMissing";

            var registryType = registry.GetType();
            var runnersField = registryType.GetField("_runners", BindingFlags.Instance | BindingFlags.NonPublic);
            if (runnersField?.GetValue(registry) is not System.Collections.IDictionary runners) return "runnersMissing";
            if (!runners.Contains(actorId)) return "runnerNotFound";
            if (runners[actorId] is not SkillPipelineRunner runner) return "runnerTypeMismatch";

            var runnerType = typeof(SkillPipelineRunner);
            var runningField = runnerType.GetField("_running", BindingFlags.Instance | BindingFlags.NonPublic);
            if (runningField?.GetValue(runner) is not System.Collections.IEnumerable entries) return "entriesMissing";

            foreach (var entryObject in entries)
            {
                var entryType = entryObject.GetType();
                var contextField = entryType.GetField("Context", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var runField = entryType.GetField("Run", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var stageField = entryType.GetField("Stage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var entryContext = contextField?.GetValue(entryObject) as SkillPipelineContext;
                if (entryContext == null || entryContext.SkillSlot != slot) continue;

                var runObject = runField?.GetValue(entryObject);
                var runType = runObject?.GetType();
                var runContext = runType?.GetProperty("Context", BindingFlags.Instance | BindingFlags.Public)?.GetValue(runObject) as SkillPipelineContext;
                var runState = runType?.GetProperty("State", BindingFlags.Instance | BindingFlags.Public)?.GetValue(runObject)?.ToString() ?? "null";
                var sameContext = ReferenceEquals(entryContext, runContext);
                var stage = stageField?.GetValue(entryObject)?.ToString() ?? "unknown";
                var entryElapsedMs = (int)Math.Round(entryContext.ElapsedTime * 1000d);
                var runElapsedMs = runContext != null ? (int)Math.Round(runContext.ElapsedTime * 1000d) : -1;
                var entryNextEventIndex = entryContext.TimelineNextEventIndex;
                var runNextEventIndex = runContext != null ? runContext.TimelineNextEventIndex : -1;
                var phaseId = string.Empty;
                if (runContext != null)
                {
                    var currentPhaseId = runContext.GetType().GetProperty("CurrentPhaseId", BindingFlags.Instance | BindingFlags.Public)?.GetValue(runContext);
                    phaseId = currentPhaseId?.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(currentPhaseId)?.ToString() ?? string.Empty;
                }

                return $"stage={stage}, runState={runState}, sameContext={sameContext}, entryElapsedMs={entryElapsedMs}, runElapsedMs={runElapsedMs}, entryNextEventIndex={entryNextEventIndex}, runNextEventIndex={runNextEventIndex}, phase={phaseId}";
            }

            return "entryNotFound";
        }

        private string DescribeWorldComposition()
        {
            var worldId = World.Id.Value;
            if (string.IsNullOrEmpty(worldId)) return "worldIdMissing";
            if (!AbilityKit.Ability.World.Diagnostics.WorldDebugRegistry.TryGet(worldId, out var report) || report == null)
            {
                return $"reportMissing:{worldId}";
            }

            var hasBootstrap = false;
            var hasSkillPipelineInstaller = false;
            for (var i = 0; i < report.Installers.Count; i++)
            {
                var installer = report.Installers[i] ?? string.Empty;
                if (installer.IndexOf("MobaWorldBootstrapModule", StringComparison.Ordinal) >= 0) hasBootstrap = true;
                if (installer.IndexOf("ConfirmedAuthorityWorldInstaller", StringComparison.Ordinal) >= 0) hasSkillPipelineInstaller = true;
            }

            return $"id={worldId}, type={report.WorldType}, modules={report.Modules.Count}, installers={report.Installers.Count}, services={report.RegisteredServices.Count}, hasMobaBootstrap={hasBootstrap}, hasConfirmedAuthorityInstaller={hasSkillPipelineInstaller}";
        }

        public void AssertActorHp(int actorId, float expectedHp, float tolerance = 0.01f, string comparator = "eq", string message = null)
        {
            var actualHp = GetActorHp(actorId, message);
            if (string.Equals(comparator, "ne", StringComparison.OrdinalIgnoreCase)
                || string.Equals(comparator, "neq", StringComparison.OrdinalIgnoreCase)
                || string.Equals(comparator, "not_eq", StringComparison.OrdinalIgnoreCase))
            {
                Assert.IsTrue(System.Math.Abs(actualHp - expectedHp) > tolerance, message ?? $"Actor hp should differ from {expectedHp}, actual={actualHp}, tolerance={tolerance}");
                return;
            }

            switch (comparator?.Trim().ToLowerInvariant())
            {
                case "gt":
                    Assert.Greater(actualHp, expectedHp, message ?? $"Actor hp should be greater than {expectedHp}, actual={actualHp}");
                    return;
                case "gte":
                case "ge":
                    Assert.GreaterOrEqual(actualHp, expectedHp, message ?? $"Actor hp should be greater or equal to {expectedHp}, actual={actualHp}");
                    return;
                case "lt":
                    Assert.Less(actualHp, expectedHp, message ?? $"Actor hp should be less than {expectedHp}, actual={actualHp}");
                    return;
                case "lte":
                case "le":
                    Assert.LessOrEqual(actualHp, expectedHp, message ?? $"Actor hp should be less or equal to {expectedHp}, actual={actualHp}");
                    return;
                default:
                    Assert.LessOrEqual(System.Math.Abs(actualHp - expectedHp), tolerance, message ?? $"Actor hp mismatch: actual={actualHp}, expected={expectedHp}, tolerance={tolerance}");
                    return;
            }
        }

        public int SpawnScenarioActor(
            string alias,
            int actorId,
            string kind,
            int teamId,
            int heroId,
            int attributeTemplateId,
            int level,
            int unitSubType,
            int mainType,
            string ownerPlayerId,
            int ownerActorId,
            string sourceKind,
            int sourceId,
            MobaAcceptanceVector3Expectation position)
        {
            var resolvedMainType = mainType != 0 ? (EntityMainType)mainType : EntityMainType.Unit;
            var resolvedUnitSubType = unitSubType != 0 ? (UnitSubType)unitSubType : UnitSubType.Hero;
            var entityKind = ResolveEntityKind(kind, resolvedMainType, resolvedUnitSubType);
            var transform = new Transform3(ToVec3(position), Quat.Identity, Vec3.One);
            var info = new MobaEntityInfo(
                actorId: actorId,
                kind: entityKind,
                transform: transform,
                team: (Team)(teamId > 0 ? teamId : 1),
                mainType: resolvedMainType,
                unitSubType: resolvedUnitSubType,
                ownerPlayer: new PlayerId(string.IsNullOrEmpty(ownerPlayerId) ? PlayerId.Value : ownerPlayerId),
                templateId: heroId > 0 ? heroId : 1);
            var spec = new MobaActorBuildSpec(
                in info,
                ResolveBuildSourceKind(sourceKind),
                sourceId,
                ownerActorId);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.AllocateActorIdIfMissing = actorId <= 0;
            request.Initializer = (entity, buildSpec) => World.Services.Resolve<ActorEntityInitPipeline>().InitializeFromAttributeTemplate(entity, attributeTemplateId > 0 ? attributeTemplateId : 1001);

            var spawn = World.Services.Resolve<IMobaActorSpawnService>();
            Assert.IsTrue(spawn.TrySpawn(in request, out var result), $"Scenario spawn_actor failed. alias={alias} actorId={actorId} error={result.Error}");
            RegisterActorAlias(alias, result.ActorId);
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
            if (string.Equals(property, "hp", StringComparison.OrdinalIgnoreCase))
            {
                attrs.Hp = value;
                return;
            }

            if (string.Equals(property, "mana", StringComparison.OrdinalIgnoreCase))
            {
                attrs.Mana = value;
                return;
            }

            if (string.Equals(property, "rage", StringComparison.OrdinalIgnoreCase))
            {
                attrs.Rage = value;
                return;
            }

            Assert.IsFalse(string.IsNullOrEmpty(property), "set_attr requires property.");
            var normalized = property.Replace(".", "_").Replace("-", "_");
            Assert.IsTrue(Enum.TryParse(normalized, ignoreCase: true, out BattleAttributeType type) && type != BattleAttributeType.None, $"Unsupported set_attr property: {property}");
            attrs.SetBase(type, value);
        }

        public void AddScenarioBuff(int targetActorId, int buffId, int sourceActorId, int durationOverrideMs)
        {
            Assert.Greater(targetActorId, 0, "add_buff requires target actor.");
            Assert.Greater(buffId, 0, "add_buff requires buffId.");
            var buffs = World.Services.Resolve<MobaBuffService>();
            Assert.IsTrue(buffs.ApplyBuffImmediate(targetActorId, buffId, sourceActorId, durationOverrideMs), $"add_buff failed. targetActorId={targetActorId} buffId={buffId} sourceActorId={sourceActorId}");
        }

        public void RemoveScenarioBuff(int targetActorId, int buffId, int sourceActorId, bool removeAll)
        {
            Assert.Greater(targetActorId, 0, "remove_buff requires target actor.");
            Assert.Greater(buffId, 0, "remove_buff requires buffId.");
            var buffs = World.Services.Resolve<MobaBuffService>();
            if (removeAll)
            {
                buffs.RemoveBuffsImmediate(targetActorId, buffId, sourceActorId, removeAll: true, TraceLifecycleReason.Dispelled);
                return;
            }

            buffs.RemoveBuffImmediate(targetActorId, buffId, sourceActorId, TraceLifecycleReason.Dispelled);
        }

        public void Tick(int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                TickWorld(FixedDelta);
            }
        }

        public void TickSeconds(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Accelerated test delta time must be finite and non-negative.");
            }

            if (deltaTime <= 0f) return;
            TickWorld(deltaTime);
        }

        public void TickMilliseconds(int milliseconds)
        {
            if (milliseconds <= 0) return;

            var wholeTicks = MillisecondsToTicks(milliseconds);
            if (wholeTicks > 0)
            {
                Tick(wholeTicks);
            }

            var consumedMs = (int)Math.Round(wholeTicks * FixedDelta * 1000f);
            var remainingMs = milliseconds - consumedMs;
            if (remainingMs > 0)
            {
                TickSeconds(remainingMs / 1000f);
            }
        }

        private void TickWorld(float deltaTime)
        {
            if (World.Services.TryResolve<IFrameTime>(out var time) && time is FrameTime frameTime)
            {
                frameTime.StepTo(new FrameIndex(frameTime.Frame.Value + 1), deltaTime);
            }

            World.Tick(deltaTime);
        }

        public int AssertPlayerActorBound(string message = null)
        {
            var playerActorMap = World.Services.Resolve<MobaPlayerActorMapService>();
            Assert.IsTrue(playerActorMap.TryGetActorId(PlayerId, out var actorId), message ?? $"Player actor binding missing: {PlayerId.Value}");
            return actorId;
        }

        public int AssertPlayerActorBound(PlayerId playerId, string message = null)
        {
            var playerActorMap = World.Services.Resolve<MobaPlayerActorMapService>();
            Assert.IsTrue(playerActorMap.TryGetActorId(playerId, out var actorId), message ?? $"Player actor binding missing: {playerId.Value}");
            return actorId;
        }

        public void AssertSlotSkill(int slot, int expectedSkillId)
        {
            var actorId = AssertPlayerActorBound();
            var loadout = World.Services.Resolve<MobaSkillLoadoutService>();
            Assert.IsTrue(loadout.TryGetSkillId(actorId, slot, out var slotSkillId), $"Slot {slot} skill missing for actor {actorId}.");
            Assert.AreEqual(expectedSkillId, slotSkillId);
        }

        public void AssertSkillUsesCastFlow(int skillId, int expectedCastFlowId)
        {
            Assert.IsTrue(Config.TryGetSkill(skillId, out var skill), $"Skill config missing: {skillId}");
            Assert.AreEqual(expectedCastFlowId, skill.CastFlowId);
        }

        public void AssertCastFlowContainsTimelineEffect(int castFlowId, int expectedEffectId)
        {
            Assert.IsTrue(Config.TryGetSkillFlow(castFlowId, out var flow), $"Skill flow config missing: {castFlowId}");
            Assert.IsTrue(ContainsTimelineEffect(flow.Phases, expectedEffectId), $"Skill flow {castFlowId} must contain timeline effect {expectedEffectId}.");
        }

        public void AssertProjectileConfigExists(int launcherId, int projectileId)
        {
            Assert.IsTrue(Config.TryGetProjectileLauncher(launcherId, out var launcher), $"Projectile launcher config missing: {launcherId}");
            Assert.IsNotNull(launcher);
            Assert.IsTrue(Config.TryGetProjectile(projectileId, out var projectile), $"Projectile config missing: {projectileId}");
            Assert.IsNotNull(projectile);
        }

        public TriggerPlan<object> AssertTriggerPlanContainsActions(int triggerId, params int[] actionIds)
        {
            Assert.IsTrue(TriggerPlans.TryGetPlanByTriggerId(triggerId, out var triggerPlan), $"Trigger plan missing for effect {triggerId}.");
            for (var i = 0; i < actionIds.Length; i++)
            {
                AssertPlanContainsAction(triggerPlan, actionIds[i], $"Effect {triggerId} must compile trigger action {actionIds[i]}.");
            }

            return triggerPlan;
        }

        public FrameIndex SubmitSkillPress(int slot)
        {
            return SubmitSkillInput(PlayerId, slot, SkillInputPhase.Press, targetActorId: 0, aimPos: default, aimDir: default);
        }

        public LogicWorldInputSubmitResult SubmitSkillInputAndGetResult(PlayerId playerId, int slot, SkillInputPhase phase, out FrameIndex castFrame, int targetActorId = 0, Vec3 aimPos = default, Vec3 aimDir = default)
        {
            var input = World.Services.Resolve<IMobaInputCoordinator>();
            castFrame = new FrameIndex(FrameTime.Frame.Value + 1);
            var skillInput = new SkillInputEvent(slot: slot, phase: phase, targetActorId: targetActorId, aimPos: in aimPos, aimDir: in aimDir);
            var command = new PlayerInputCommand(castFrame, playerId, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in skillInput));
            var result = input.TrySubmit(castFrame, new[] { command });
            if (!result.Succeeded || result.HandledCount > 0)
            {
                return result;
            }

            var playerActorMap = World.Services.Resolve<MobaPlayerActorMapService>();
            if (!playerActorMap.TryGetActorId(playerId, out var actorId))
            {
                return result;
            }

            var skills = World.Services.Resolve<SkillCastCoordinator>();
            var direct = skills.TryHandleInputResult(actorId, in skillInput);
            var directFailure = direct.Failure.HasValue ? direct.Failure.ToString() : "<none>";
            var message = string.IsNullOrEmpty(result.Message)
                ? $"DirectSkillResult=Success:{direct.Success},Code={direct.Code},Message={direct.Message},Failure={directFailure}"
                : result.Message + $", DirectSkillResult=Success:{direct.Success},Code={direct.Code},Message={direct.Message},Failure={directFailure}";
            return LogicWorldInputSubmitResult.Accepted(result.AcceptedCount, result.HandledCount, message);
        }

        public FrameIndex SubmitSkillInput(PlayerId playerId, int slot, SkillInputPhase phase, int targetActorId = 0, Vec3 aimPos = default, Vec3 aimDir = default)
        {
            SubmitSkillInputAndGetResult(playerId, slot, phase, out var castFrame, targetActorId, aimPos, aimDir);
            return castFrame;
        }

        public LogicWorldInputSubmitResult SubmitSkillInputAndGetResult(string actorAlias, int slot, string phase, out FrameIndex castFrame, string targetAlias = null, int targetActorId = 0, MobaAcceptanceVector3Expectation position = null, MobaAcceptanceVector3Expectation direction = null)
        {
            Assert.IsTrue(TryGetPlayerId(actorAlias, out var playerId), $"Actor alias {actorAlias} is not bound to a player input source.");
            var resolvedTargetActorId = targetActorId;
            if (resolvedTargetActorId <= 0 && !string.IsNullOrEmpty(targetAlias))
            {
                Assert.IsTrue(TryGetActorId(targetAlias, out resolvedTargetActorId), $"Target actor alias missing: {targetAlias}");
            }

            var inputPhase = ParseSkillInputPhase(phase);
            // Formal HUD input sends aim data on release/targeted submission, not on a plain press.
            // Normalize directional-only press steps so acceptance scenarios match runtime input semantics.
            var shouldIgnorePressAim = inputPhase == SkillInputPhase.Press
                && resolvedTargetActorId <= 0
                && IsZeroVectorExpectation(position)
                && !IsZeroVectorExpectation(direction);
            var aimPos = shouldIgnorePressAim ? default : ToVec3(position);
            var aimDir = shouldIgnorePressAim ? default : ToVec3(direction);
            return SubmitSkillInputAndGetResult(playerId, slot, inputPhase, out castFrame, resolvedTargetActorId, aimPos, aimDir);
        }

        public FrameIndex SubmitSkillInput(string actorAlias, int slot, string phase, string targetAlias = null, int targetActorId = 0, MobaAcceptanceVector3Expectation position = null, MobaAcceptanceVector3Expectation direction = null)
        {
            SubmitSkillInputAndGetResult(actorAlias, slot, phase, out var castFrame, targetAlias, targetActorId, position, direction);
            return castFrame;
        }

        public void CastSkillSlotAndTick(int slot, int postCastTicks = 25)
        {
            SubmitSkillPress(slot);
            Tick(postCastTicks);
        }

        public TraceSnapshot<MobaTraceMetadata> CastSkillSlotAndTickUntilEffect(int slot, int skillId, int effectId, int safetyFrames = 5, int maxExtraFrames = 30)
        {
            var effectTimeMs = CalculateWaitMillisecondsForSkillEffect(skillId, effectId);
            var safetySeconds = Math.Max(0, safetyFrames) * FixedDelta;
            var maxTicks = CalculateWaitTicksForSkillEffect(skillId, effectId, safetyFrames) + maxExtraFrames;

            SubmitSkillPress(slot);

            // Keep input ordering frame-accurate: the command is submitted for Frame + 1,
            // so the first tick uses the normal fixed delta to let the cast enter runtime systems.
            Tick(1);
            if (TryFindTraceNode(MobaTraceKind.EffectExecution, effectId, out var existing)) return existing;

            // Unit tests do not need to spend N fixed frames to reach a delayed timeline point.
            // Convert the configured wait into elapsed seconds and advance the runtime once with
            // the equivalent delta. A small fixed-frame safety window remains as a fallback for
            // side effects that flush on the following frame.
            var acceleratedSeconds = Math.Max(0f, effectTimeMs / 1000f - FixedDelta) + safetySeconds;
            TickSeconds(acceleratedSeconds);
            if (TryFindTraceNode(MobaTraceKind.EffectExecution, effectId, out var accelerated)) return accelerated;

            return TickUntilTraceNode(MobaTraceKind.EffectExecution, effectId, maxExtraFrames, $"EffectExecution trace missing for effect {effectId} after casting skill {skillId} slot {slot} within accelerated {effectTimeMs} ms plus {maxExtraFrames} fallback ticks; fixed-frame equivalent timeout was {maxTicks} ticks.");
        }

        public long CaptureTraceBaseline()
        {
            var latestContextId = 0L;
            foreach (MobaTraceKind kind in Enum.GetValues(typeof(MobaTraceKind)))
            {
                if (kind == MobaTraceKind.None) continue;
                foreach (var node in Trace.GetNodesByKind((int)kind))
                {
                    if (node.ContextId > latestContextId) latestContextId = node.ContextId;
                }
            }

            return latestContextId;
        }

        public TraceSnapshot<MobaTraceMetadata> TickUntilTraceNode(MobaTraceKind kind, int configId, int maxTicks, string message)
        {
            if (TryFindTraceNode(kind, configId, out var existing)) return existing;

            for (var i = 0; i < maxTicks; i++)
            {
                Tick(1);
                if (TryFindTraceNode(kind, configId, out var node)) return node;
            }

            Assert.Fail(message);
            return default;
        }

        public TraceSnapshot<MobaTraceMetadata> TickUntilTraceNodeAfter(long baselineContextId, MobaTraceKind kind, int configId, int maxTicks, string message)
        {
            if (TryFindTraceNodeAfter(baselineContextId, kind, configId, out var existing)) return existing;

            for (var i = 0; i < maxTicks; i++)
            {
                Tick(1);
                if (TryFindTraceNodeAfter(baselineContextId, kind, configId, out var node)) return node;
            }

            Assert.Fail(message);
            return default;
        }

        public TraceSnapshot<MobaTraceMetadata> TickUntilTraceNodeInRoot(long rootId, MobaTraceKind kind, int configId, int maxTicks, string message)
        {
            if (TryFindTraceNodeInRoot(rootId, kind, configId, out var existing)) return existing;

            for (var i = 0; i < maxTicks; i++)
            {
                Tick(1);
                if (TryFindTraceNodeInRoot(rootId, kind, configId, out var node)) return node;
            }

            Assert.Fail(message);
            return default;
        }

        public int CalculateWaitTicksForSkillEffect(int skillId, int effectId, int safetyFrames = 5)
        {
            var effectTimeMs = CalculateWaitMillisecondsForSkillEffect(skillId, effectId);
            return MillisecondsToTicks(effectTimeMs) + Math.Max(0, safetyFrames);
        }

        public int CalculateWaitTicksForCastFlowEffect(int castFlowId, int effectId, int safetyFrames = 5)
        {
            var effectTimeMs = CalculateWaitMillisecondsForCastFlowEffect(castFlowId, effectId);
            return MillisecondsToTicks(effectTimeMs) + Math.Max(0, safetyFrames);
        }

        public int CalculateWaitMillisecondsForSkillEffect(int skillId, int effectId)
        {
            Assert.IsTrue(Config.TryGetSkill(skillId, out var skill), $"Skill config missing: {skillId}");
            return CalculateWaitMillisecondsForCastFlowEffect(skill.CastFlowId, effectId);
        }

        public int CalculateWaitMillisecondsForCastFlowEffect(int castFlowId, int effectId)
        {
            Assert.IsTrue(Config.TryGetSkillFlow(castFlowId, out var flow), $"Skill flow config missing: {castFlowId}");
            Assert.IsTrue(TryGetEarliestEffectTimeMs(flow.Phases, effectId, out var effectTimeMs), $"Skill flow {castFlowId} must contain timeline effect {effectId}.");
            return Math.Max(0, effectTimeMs);
        }

        public TraceSnapshot<MobaTraceMetadata> AssertSkillCastTrace(int skillId)
        {
            return AssertTraceNode(MobaTraceKind.SkillCast, skillId, $"SkillCast trace missing for skill {skillId}.");
        }

        public TraceSnapshot<MobaTraceMetadata> AssertEffectExecutionTrace(int effectId)
        {
            return AssertTraceNode(MobaTraceKind.EffectExecution, effectId, $"EffectExecution trace missing for configured effect {effectId}.");
        }

        public TraceSnapshot<MobaTraceMetadata> AssertActionExecutedUnderEffect(long effectRootId, int actionId, string actionName = null)
        {
            var displayName = string.IsNullOrEmpty(actionName) ? actionId.ToString() : $"{actionName}({actionId})";
            return AssertTraceNodeInRoot(effectRootId, MobaTraceKind.EffectAction, actionId, $"Trigger action {displayName} was not executed under effect root {effectRootId}.");
        }

        public TraceSnapshot<MobaTraceMetadata> AssertProjectileLaunchedUnderEffect(long effectRootId, int launcherId, int projectileId)
        {
            return AssertTraceNodeInRoot(effectRootId, MobaTraceKind.ProjectileLaunch, projectileId, $"shoot_projectile did not launch configured projectile {projectileId} from launcher {launcherId}.");
        }

        public TraceSnapshot<MobaTraceMetadata> AssertAreaSpawnedUnderEffect(long effectRootId, int areaTemplateId)
        {
            return AssertTraceNodeInRoot(effectRootId, MobaTraceKind.AreaSpawn, areaTemplateId, $"spawn_area did not spawn configured area template {areaTemplateId} under effect root {effectRootId}.");
        }

        public int CountTraceNodesInRoot(long rootId, MobaTraceKind kind, int configId)
        {
            var count = 0;
            foreach (var node in Trace.GetNodesByRoot(rootId))
            {
                if (node.Kind == (int)kind && node.Metadata != null && node.Metadata.ConfigId == configId)
                {
                    count++;
                }
            }

            return count;
        }

        public void AssertTraceLifecycle(in TraceSnapshot<MobaTraceMetadata> parent, in TraceSnapshot<MobaTraceMetadata> child, string message)
        {
            Assert.IsTrue(parent.IsValid, message + " Parent trace must be valid.");
            Assert.IsTrue(child.IsValid, message + " Child trace must be valid.");
            Assert.AreEqual(parent.RootId, child.RootId, message + $" ParentRoot={parent.RootId}, ChildRoot={child.RootId}.");
            Assert.AreNotEqual(parent.ContextId, child.ContextId, message + " Parent and child trace contexts must differ.");
        }

        public TraceSnapshot<MobaTraceMetadata> AssertTraceNode(MobaTraceKind kind, int configId, string message)
        {
            if (TryFindTraceNode(kind, configId, out var node)) return node;

            Assert.Fail(message);
            return default;
        }

        public TraceSnapshot<MobaTraceMetadata> AssertTraceNodeInRoot(long rootId, MobaTraceKind kind, int configId, string message)
        {
            if (TryFindTraceNodeInRoot(rootId, kind, configId, out var node)) return node;

            Assert.Fail(message);
            return default;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            World.Dispose();
        }

        private static IWorld CreateHeadlessMobaWorld(
            WorldId worldId,
            string worldType,
            in MobaBattleLaunchSpec launchSpec,
            MobaRuntimeValidationMode validationMode = MobaRuntimeValidationMode.ManualOnly)
        {
            var registry = new WorldTypeRegistry().RegisterEntitasWorld(worldType);
            var manager = new WorldManager(new RegistryWorldFactory(registry));

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(MobaWorldBootstrapModule).Assembly,
                    typeof(MobaSkillConfigTestHarness).Assembly,
                    typeof(ResourcesTextAssetLoader).Assembly
                },
                new[] { "AbilityKit" });

            builder.RegisterInstance(launchSpec.ToWorldInitData(MobaWorldBootstrapModule.InitOpCode));
            builder.RegisterInstance(new MobaRuntimeValidationOptions
            {
                Mode = validationMode,
            });
            builder.RegisterInstance<AbilityKit.Ability.Config.ITextAssetLoader>((AbilityKit.Ability.Config.ITextAssetLoader)Activator.CreateInstance(typeof(ResourcesTextAssetLoader), new object[] { null }));
            builder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new FrameTime());
            builder.TryRegister<ICollisionService>(WorldLifetime.Singleton, _ => new CollisionService(new CollisionWorldOptions { BroadphaseType = BroadphaseType.Grid, GridCellSize = 4f }));

            var options = new WorldCreateOptions(worldId, worldType)
            {
                ServiceBuilder = builder,
            };
            options.Modules.Add(new MobaWorldBootstrapModule());
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());
            return manager.Create(options);
        }

        private static MobaBattleLaunchSpec BuildLaunchSpec(
            WorldId worldId,
            string worldType,
            PlayerId playerId,
            MobaPlayerLoadout[] players,
            int tickRate,
            int inputDelayFrames)
        {
            return new MobaBattleLaunchSpec(
                battleId: worldId.Value,
                matchId: worldId.Value,
                worldId: worldId.Value,
                worldType: worldType,
                clientId: "editmode_skill_config_test",
                localPlayerId: playerId,
                mapId: 1,
                gameplayId: 1,
                ruleSetId: 0,
                configVersion: 0,
                protocolVersion: 0,
                randomSeed: 123,
                tickRate: tickRate,
                inputDelayFrames: inputDelayFrames,
                launchMode: MobaBattleLaunchMode.ViewFastEnter,
                syncMode: MobaBattleLaunchSyncMode.Hybrid,
                authorityMode: MobaBattleLaunchAuthorityMode.LocalAuthority,
                players: players,
                enterGamePayload: Array.Empty<byte>());
        }

        public void ResetSkillCooldown(int actorId, int skillId, string message = null)
        {
            Assert.Greater(skillId, 0, message ?? "skillId must be positive.");
            var actor = AssertActorEntity(actorId, message ?? $"Actor entity missing: {actorId}");
            Assert.IsTrue(actor.hasSkillLoadout && actor.skillLoadout.ActiveSkills != null, message ?? $"Actor {actorId} should retain an active skill loadout.");

            for (var i = 0; i < actor.skillLoadout.ActiveSkills.Length; i++)
            {
                var runtime = actor.skillLoadout.ActiveSkills[i];
                if (runtime == null || runtime.SkillId != skillId) continue;
                runtime.CooldownEndTimeMs = 0L;
                runtime.CooldownDurationMs = 0;
                return;
            }

            Assert.Fail(message ?? $"Active skill runtime {skillId} missing for actor {actorId} while resetting cooldown.");
        }

        private bool TryFindTraceNode(MobaTraceKind kind, int configId, out TraceSnapshot<MobaTraceMetadata> match)
        {
            return TryFindTraceNodeAfter(0L, kind, configId, out match);
        }

        private bool TryFindTraceNodeAfter(long baselineContextId, MobaTraceKind kind, int configId, out TraceSnapshot<MobaTraceMetadata> match)
        {
            foreach (var node in Trace.GetNodesByKind((int)kind))
            {
                if (node.ContextId <= baselineContextId) continue;
                if (node.Metadata != null && node.Metadata.ConfigId == configId)
                {
                    match = node;
                    return true;
                }
            }

            match = default;
            return false;
        }

        private bool TryFindTraceNodeInRoot(long rootId, MobaTraceKind kind, int configId, out TraceSnapshot<MobaTraceMetadata> match)
        {
            foreach (var node in Trace.GetNodesByRoot(rootId))
            {
                if (node.Kind == (int)kind && node.Metadata != null && node.Metadata.ConfigId == configId)
                {
                    match = node;
                    return true;
                }
            }

            match = default;
            return false;
        }

        private int MillisecondsToTicks(int milliseconds)
        {
            if (milliseconds <= 0) return 1;
            return Math.Max(1, (int)Math.Ceiling(milliseconds / 1000d * Math.Max(1, TickRate)));
        }

        private static bool ContainsTimelineEffect(System.Collections.Generic.IReadOnlyList<SkillPhaseDTO> phases, int effectId)
        {
            return TryGetEarliestEffectTimeMs(phases, effectId, out _);
        }

        private static bool TryGetEarliestEffectTimeMs(System.Collections.Generic.IReadOnlyList<SkillPhaseDTO> phases, int effectId, out int earliestMs)
        {
            return TryGetEarliestEffectTimeMs(phases, effectId, 0, out earliestMs, out _);
        }

        private static bool TryGetEarliestEffectTimeMs(System.Collections.Generic.IReadOnlyList<SkillPhaseDTO> phases, int effectId, int startMs, out int earliestMs, out int durationMs)
        {
            earliestMs = int.MaxValue;
            durationMs = 0;
            if (phases == null) return false;

            var found = false;
            var cursorMs = startMs;
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase == null) continue;

                if (TryGetPhaseEffectTimeMs(phase, effectId, cursorMs, out var phaseEarliestMs, out var phaseDurationMs))
                {
                    found = true;
                    if (phaseEarliestMs < earliestMs) earliestMs = phaseEarliestMs;
                }

                cursorMs += phaseDurationMs;
                durationMs += phaseDurationMs;
            }

            return found;
        }

        private static bool TryGetPhaseEffectTimeMs(SkillPhaseDTO phase, int effectId, int startMs, out int earliestMs, out int durationMs)
        {
            earliestMs = int.MaxValue;
            durationMs = 0;
            if (phase == null) return false;

            switch ((SkillPhaseType)phase.Type)
            {
                case SkillPhaseType.Timeline:
                    return TryGetTimelineEffectTimeMs(phase.Timeline, effectId, startMs, out earliestMs, out durationMs);
                case SkillPhaseType.Delay:
                    durationMs = phase.Delay != null ? Math.Max(0, phase.Delay.DelayMs) : 0;
                    return false;
                case SkillPhaseType.WaitUntil:
                    durationMs = phase.WaitUntil != null ? Math.Max(0, phase.WaitUntil.TimeoutMs) : 0;
                    return false;
                case SkillPhaseType.Sequence:
                    return TryGetEarliestEffectTimeMs(phase.Children, effectId, startMs, out earliestMs, out durationMs);
                case SkillPhaseType.Parallel:
                    return TryGetParallelEffectTimeMs(phase.Children, effectId, startMs, out earliestMs, out durationMs);
                case SkillPhaseType.Repeat:
                    return TryGetRepeatEffectTimeMs(phase.Repeat, effectId, startMs, out earliestMs, out durationMs);
                case SkillPhaseType.RulePlan:
                    durationMs = 0;
                    return false;
                default:
                    durationMs = 0;
                    return false;
            }
        }

        private static bool TryGetTimelineEffectTimeMs(SkillTimelinePhaseDTO timeline, int effectId, int startMs, out int earliestMs, out int durationMs)
        {
            earliestMs = int.MaxValue;
            durationMs = timeline != null ? Math.Max(0, timeline.DurationMs) : 0;
            var found = false;
            var events = timeline != null ? timeline.Events : null;
            if (events == null) return false;

            for (var i = 0; i < events.Length; i++)
            {
                var e = events[i];
                if (e == null) continue;

                var atMs = Math.Max(0, e.AtMs);
                if (atMs > durationMs) durationMs = atMs;
                if (e.EffectId != effectId) continue;

                found = true;
                var absoluteMs = startMs + atMs;
                if (absoluteMs < earliestMs) earliestMs = absoluteMs;
            }

            return found;
        }

        private static bool TryGetParallelEffectTimeMs(System.Collections.Generic.IReadOnlyList<SkillPhaseDTO> children, int effectId, int startMs, out int earliestMs, out int durationMs)
        {
            earliestMs = int.MaxValue;
            durationMs = 0;
            if (children == null) return false;

            var found = false;
            for (var i = 0; i < children.Count; i++)
            {
                if (TryGetPhaseEffectTimeMs(children[i], effectId, startMs, out var childEarliestMs, out var childDurationMs))
                {
                    found = true;
                    if (childEarliestMs < earliestMs) earliestMs = childEarliestMs;
                }

                if (childDurationMs > durationMs) durationMs = childDurationMs;
            }

            return found;
        }

        private static bool TryGetRepeatEffectTimeMs(SkillRepeatPhaseDTO repeat, int effectId, int startMs, out int earliestMs, out int durationMs)
        {
            earliestMs = int.MaxValue;
            durationMs = 0;
            if (repeat == null || repeat.Phase == null || repeat.RepeatCount <= 0) return false;

            var intervalMs = Math.Max(0, repeat.IntervalMs);
            var found = false;
            var cursorMs = startMs;
            for (var i = 0; i < repeat.RepeatCount; i++)
            {
                if (TryGetPhaseEffectTimeMs(repeat.Phase, effectId, cursorMs, out var childEarliestMs, out var childDurationMs))
                {
                    found = true;
                    if (childEarliestMs < earliestMs) earliestMs = childEarliestMs;
                }

                cursorMs += childDurationMs;
                if (i < repeat.RepeatCount - 1) cursorMs += intervalMs;
                durationMs += childDurationMs;
                if (i < repeat.RepeatCount - 1) durationMs += intervalMs;
            }

            return found;
        }

        private void CacheScenarioAliases(MobaAcceptanceActorExpectation[] scenarioActors)
        {
            if (scenarioActors == null) return;

            for (var i = 0; i < scenarioActors.Length; i++)
            {
                var actor = scenarioActors[i];
                if (actor == null) continue;

                if (!string.IsNullOrEmpty(actor.alias))
                {
                    if (!string.IsNullOrEmpty(actor.playerId))
                    {
                        var typedPlayerId = new PlayerId(actor.playerId);
                        _aliasToPlayerId[actor.alias] = typedPlayerId;
                        _aliasToPlayerId[actor.playerId] = typedPlayerId;
                    }

                    if (TryParseActorId(actor.actorId, out var actorId))
                    {
                        _aliasToActorId[actor.alias] = actorId;
                    }
                }

                if (!string.IsNullOrEmpty(actor.playerId))
                {
                    var typedPlayerId = new PlayerId(actor.playerId);
                    _aliasToPlayerId[actor.playerId] = typedPlayerId;
                    if (TryParseActorId(actor.actorId, out var actorId))
                    {
                        _aliasToActorId[actor.playerId] = actorId;
                    }
                }
            }
        }

        public void RefreshScenarioActorAliases()
        {
            if (_scenarioActors == null || _scenarioActors.Length == 0) return;

            var playerActorMap = World.Services.Resolve<MobaPlayerActorMapService>();
            for (var i = 0; i < _scenarioActors.Length; i++)
            {
                var actor = _scenarioActors[i];
                if (actor == null || string.IsNullOrEmpty(actor.playerId)) continue;

                var typedPlayerId = new PlayerId(actor.playerId);
                if (!string.IsNullOrEmpty(actor.alias)) _aliasToPlayerId[actor.alias] = typedPlayerId;
                _aliasToPlayerId[actor.playerId] = typedPlayerId;
                if (playerActorMap.TryGetActorId(typedPlayerId, out var actorId) && actorId > 0)
                {
                    if (!string.IsNullOrEmpty(actor.alias)) _aliasToActorId[actor.alias] = actorId;
                    _aliasToActorId[actor.playerId] = actorId;
                }
            }
        }

        private void RepairScenarioPlayerActorBindings()
        {
            if (_scenarioActors == null || _scenarioActors.Length == 0) return;
            if (!World.Services.TryResolve<MobaPlayerActorMapService>(out var playerActorMap) || playerActorMap == null) return;

            for (var i = 0; i < _scenarioActors.Length; i++)
            {
                var actor = _scenarioActors[i];
                if (actor == null || string.IsNullOrEmpty(actor.playerId)) continue;

                var typedPlayerId = new PlayerId(actor.playerId);
                if (playerActorMap.TryGetActorId(typedPlayerId, out var mappedActorId) && mappedActorId > 0) continue;

                if (!TryResolveScenarioActorId(actor, out var actorId)) continue;
                playerActorMap.Bind(typedPlayerId, actorId);
            }
        }

        private bool TryResolveScenarioActorId(MobaAcceptanceActorExpectation actor, out int actorId)
        {
            actorId = 0;
            if (actor == null) return false;

            if (!string.IsNullOrEmpty(actor.alias) && _aliasToActorId.TryGetValue(actor.alias, out actorId) && actorId > 0)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(actor.playerId) && _aliasToActorId.TryGetValue(actor.playerId, out actorId) && actorId > 0)
            {
                return true;
            }

            if (TryParseActorId(actor.actorId, out actorId) && actorId > 0)
            {
                return true;
            }

            if (string.IsNullOrEmpty(actor.playerId)) return false;

            var registry = World.Services.Resolve<MobaActorRegistry>();
            foreach (var entry in registry.Entries)
            {
                var candidateActorId = entry.Key;
                var entity = entry.Value;
                if (entity == null || !entity.hasOwnerPlayerId) continue;
                if (!entity.ownerPlayerId.Value.Equals(new PlayerId(actor.playerId))) continue;
                actorId = candidateActorId;
                return actorId > 0;
            }

            return false;
        }

        private static string ResolvePrimaryPlayerId(MobaAcceptanceExpectation expectation, MobaAcceptanceActorExpectation[] scenarioActors)
        {
            if (expectation != null && expectation.input != null && !string.IsNullOrEmpty(expectation.input.playerId)) return expectation.input.playerId;

            if (scenarioActors != null)
            {
                for (var i = 0; i < scenarioActors.Length; i++)
                {
                    var actor = scenarioActors[i];
                    if (actor != null && !string.IsNullOrEmpty(actor.playerId)) return actor.playerId;
                }
            }

            return DefaultPlayerId;
        }

        private static MobaPlayerLoadout[] BuildPlayerLoadouts(MobaAcceptanceExpectation expectation, MobaAcceptanceActorExpectation[] scenarioActors, PlayerId fallbackPlayerId, int inputDelayFrames)
        {
            var players = new List<MobaPlayerLoadout>();
            if (scenarioActors != null)
            {
                for (var i = 0; i < scenarioActors.Length; i++)
                {
                    var actor = scenarioActors[i];
                    if (actor == null || string.IsNullOrEmpty(actor.playerId)) continue;

                    var skillIds = ResolveActorSkillIds(expectation, actor);
                    var heroId = actor.heroId > 0 ? actor.heroId : 1;
                    var attributeTemplateId = actor.attributeTemplateId > 0 ? actor.attributeTemplateId : 1001;
                    if (skillIds.Length == 0)
                    {
                        skillIds = ResolveDefaultSkillIds(heroId, attributeTemplateId);
                    }

                    var spawnPosition = actor.spawnPosition;
                    var hasSpawnPosition = actor.hasSpawnPosition || spawnPosition != null ? 1 : 0;
                    players.Add(new MobaPlayerLoadout(
                        new PlayerId(actor.playerId),
                        teamId: actor.teamId > 0 ? actor.teamId : 1,
                        heroId: heroId,
                        attributeTemplateId: attributeTemplateId,
                        level: actor.level > 0 ? actor.level : 1,
                        basicAttackSkillId: actor.basicAttackSkillId > 0
                            ? actor.basicAttackSkillId
                            : ResolveBasicAttackSkillId(heroId, attributeTemplateId),
                        skillIds: skillIds,
                        spawnIndex: actor.spawnIndex,
                        unitSubType: actor.unitSubType != 0 ? actor.unitSubType : (int)UnitSubType.Hero,
                        mainType: actor.mainType != 0 ? actor.mainType : (int)EntityMainType.Unit,
                        hasSpawnPosition: hasSpawnPosition,
                        spawnX: spawnPosition != null ? spawnPosition.x : 0f,
                        spawnY: spawnPosition != null ? spawnPosition.y : 0f,
                        spawnZ: spawnPosition != null ? spawnPosition.z : 0f));
                }
            }

            if (players.Count == 0)
            {
                var skillIds = expectation != null && expectation.config != null && expectation.config.skillId > 0
                    ? new[] { expectation.config.skillId }
                    : Array.Empty<int>();
                players.Add(new MobaPlayerLoadout(
                    fallbackPlayerId,
                    teamId: 1,
                    heroId: 1,
                    attributeTemplateId: 1001,
                    level: 1,
                    basicAttackSkillId: 1,
                    skillIds: skillIds,
                    spawnIndex: 0,
                    unitSubType: (int)UnitSubType.Hero,
                    mainType: (int)EntityMainType.Unit,
                    hasSpawnPosition: 1,
                    spawnX: 0f,
                    spawnY: 0f,
                    spawnZ: 0f));
            }

            return players.ToArray();
        }

        private static int ResolveBasicAttackSkillId(int heroId, int attributeTemplateId)
        {
            if (heroId == 1001 || attributeTemplateId == 1001) return 10010001;
            if (heroId == 1002 || attributeTemplateId == 1002) return 10020001;
            if (heroId == 1003 || attributeTemplateId == 1003) return 10030001;
            if (heroId == 1004 || attributeTemplateId == 1004) return 10040011;
            if (heroId == 1005 || attributeTemplateId == 1005) return 10050001;
            if (heroId == 1006 || attributeTemplateId == 1006) return 10060001;
            return 1;
        }

        private static int[] ResolveDefaultSkillIds(int heroId, int attributeTemplateId)
        {
            if (heroId == 1002 || attributeTemplateId == 1002)
            {
                return new[] { 10020101, 10020201, 10020301 };
            }

            return Array.Empty<int>();
        }

        private static Vec3 ToVec3(MobaAcceptanceVector3Expectation value)
        {
            return value == null ? Vec3.Zero : new Vec3(value.x, value.y, value.z);
        }

        private static bool IsZeroVectorExpectation(MobaAcceptanceVector3Expectation value)
        {
            return value == null || (Math.Abs(value.x) <= float.Epsilon && Math.Abs(value.y) <= float.Epsilon && Math.Abs(value.z) <= float.Epsilon);
        }

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

        private static SkillInputPhase ParseSkillInputPhase(string phase)
        {
            if (string.IsNullOrEmpty(phase)) return SkillInputPhase.Press;
            if (string.Equals(phase, "press", StringComparison.OrdinalIgnoreCase)) return SkillInputPhase.Press;
            if (string.Equals(phase, "hold", StringComparison.OrdinalIgnoreCase)) return SkillInputPhase.Hold;
            if (string.Equals(phase, "release", StringComparison.OrdinalIgnoreCase)) return SkillInputPhase.Release;
            if (string.Equals(phase, "cancel", StringComparison.OrdinalIgnoreCase)) return SkillInputPhase.Cancel;
            return (SkillInputPhase)Enum.Parse(typeof(SkillInputPhase), phase, ignoreCase: true);
        }

        private static int[] ResolveActorSkillIds(MobaAcceptanceExpectation expectation, MobaAcceptanceActorExpectation actor)
        {
            if (actor != null)
            {
                if (actor.skillIds != null && actor.skillIds.Length > 0) return actor.skillIds;
                if (actor.carriedSkillIds != null && actor.carriedSkillIds.Length > 0) return actor.carriedSkillIds;
            }

            if (expectation != null && expectation.config != null && expectation.config.skillId > 0)
            {
                return new[] { expectation.config.skillId };
            }

            return Array.Empty<int>();
        }

        private static MobaAcceptanceActorExpectation[] GetScenarioActors(MobaAcceptanceExpectation expectation)
        {
            if (expectation == null) return Array.Empty<MobaAcceptanceActorExpectation>();
            if (expectation.scenario != null && expectation.scenario.actors != null && expectation.scenario.actors.Length > 0) return expectation.scenario.actors;
            if (expectation.actors != null && expectation.actors.Length > 0) return expectation.actors;
            return Array.Empty<MobaAcceptanceActorExpectation>();
        }

        private static bool TryParseActorId(string actorId, out int value)
        {
            return int.TryParse(actorId, out value) && value > 0;
        }

        private static void AssertPlanContainsAction(TriggerPlan<object> plan, int actionId, string message)
        {
            var actions = plan.Actions;
            if (actions != null)
            {
                for (var i = 0; i < actions.Length; i++)
                {
                    if ((int)actions[i].Id.Value == actionId)
                    {
                        return;
                    }
                }
            }

            Assert.Fail($"{message} Actual actions: {DescribeActionIds(actions)}");
        }

        private static string DescribeActionIds(ActionCallPlan[] actions)
        {
            if (actions == null) return "<null>";
            if (actions.Length == 0) return "<empty>";

            var ids = new List<string>(actions.Length);
            for (var i = 0; i < actions.Length; i++)
            {
                ids.Add(((int)actions[i].Id.Value).ToString());
            }

            return string.Join(", ", ids);
        }
    }
}
