using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Moba;
using AbilityKit.Trace;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 在两个宿主中复用真实技能输入、自然检查点与逐帧恢复断言。
    /// </summary>
    internal static partial class PublishedHeroPredictionScenario
    {
        internal static (int Hero, int Slot, int Skill, Vec3[] Targets, Vec3 Aim) Describe(string skill) => skill switch
        {
            "LuxQ" => (3007, 1, 30070201, new[] { new Vec3(100, 0, 105), new Vec3(100, 0, 108), new Vec3(100, 0, 111) }, new Vec3(100, 0, 113)),
            "KarthusE" => (3006, 3, 30060401, new[] { new Vec3(100, 0, 103) }, new Vec3(100, 0, 103)),
            "KarthusR" => (3006, 4, 30060501, new[] { new Vec3(200, 0, 200), new Vec3(202, 0, 200) }, new Vec3(100, 0, 101)),
            "AnnieR" => (3003, 4, 30030501, new[] { new Vec3(100, 0, 104) }, new Vec3(100, 0, 104)),
            "AnnieRExpiry" => (3003, 4, 30030501, Array.Empty<Vec3>(), new Vec3(100, 0, 104)),
            "VeigarE" => (3004, 3, 30040401, new[] { new Vec3(105, 0, 106) }, new Vec3(100, 0, 106)),
            _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, null)
        };

        internal static int[] Loadout(int hero) => Enumerable.Range(1, 5).Select(slot => hero * 10000 + slot * 100 + 1).ToArray();

        internal static string TargetPlayerName(int index) => $"target_{index}";

        internal const float SweptCrossingFixtureMoveSpeed = 108f;

        internal static void Run(string skill, IWorld world, IWorldManager worlds, PlayerId player, int casterId,
            int[] targets, float fixedDelta, Action<string> report)
        {
            if (skill == "VeigarE")
            {
                CheckEventHorizon(world, worlds, player, casterId, targets.Single(), fixedDelta, report);
                return;
            }
            var scenario = Describe(skill);
            var services = world.Services;
            var actors = services.Resolve<MobaActorRegistry>();
            var gate = services.Resolve<MobaLogicWorldRunGateService>();
            var frameTime = services.Resolve<IFrameTime>();
            var trace = services.Resolve<MobaTraceRegistry>();
            var links = services.Resolve<MobaProjectileLinkService>();
            var summons = services.Resolve<MobaSummonService>();
            var subscriptions = services.Resolve<MobaTriggerPlanSubscriptionService>();
            var continuous = services.Resolve<IMobaContinuousRuntimeQueryService>();
            var calculator = new MobaAuthoritativeStateHashCalculator();
            var observations = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(world), new RollbackSnapshotRingBuffer(4));
            using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
            var options = new HostRuntimeOptions();
            var host = new HostRuntime(worlds, options);
            var limit = frameTime.Frame.Value;
            var mismatch = -1;
            var sequence = 0;
            var aim = scenario.Aim;
            var initialHp = targets.Select(Hp).ToArray();
            var passiveOwners = PassiveOwners();
            uint BattleHash() => calculator.Compute(gate.InGame, actors);
            var prediction = new ClientPredictionDriverModule(_ => remote, _ => null,
                resolveIdealFrameLimit: _ => limit, minPredictionWindow: 4, enableRollback: true,
                buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction,
                buildComputeHash: _ => frame => new WorldStateHash(BattleHash() ^ (frame.Value == mismatch ? 1u : 0u)));
            prediction.Install(host, options);
            try
            {
                options.WorldCreated.Invoke(world);
                Require(prediction.TryRebase(world.Id, frameTime.Frame), "initial rebase failed");
                if (skill == "AnnieRExpiry")
                {
                    CheckAnnieRExpiry();
                    return;
                }
                Cast();
                Until(CheckpointIsLive, 100, "live checkpoint");
                var checkpoint = limit;
                var baseline = new Dictionary<int, (WorldRollbackSnapshot Snapshot, uint Hash)> { [checkpoint] = Observe(checkpoint) };
                var maxLiveFrames = skill == "KarthusE" ? 30 : 3;
                for (var step = 1; step <= maxLiveFrames; step++)
                {
                    var frame = checkpoint + step;
                    mismatch = step <= 2 ? frame : -1;
                    Push();
                    Require(CheckpointIsLive(), "checkpoint state ended during the live replay window");
                    baseline.Add(frame, Observe(frame));
                    if (step >= 3 && (skill != "KarthusE" || SkillHits() >= 2)) break;
                }
                var liveThrough = limit;
                var hitsThroughReplay = SkillHits();
                Require(skill != "KarthusE" || hitsThroughReplay == 2, "the E replay window must cover two ticks");
                var hpThroughReplay = targets.Select(Hp).ToArray();

                mismatch = checkpoint + 2;
                Correct(checkpoint, 1);
                mismatch = -1;
                Correct(checkpoint + 1, 2);
                Require(SkillHits() == hitsThroughReplay && targets.Select(Hp).SequenceEqual(hpThroughReplay),
                    "replay must reproduce hit counts and target HP");
                report($"{skill}: two corrections at {checkpoint}/{checkpoint + 1} through {liveThrough}; " +
                    $"all {baseline[checkpoint].Snapshot.Entries.Length} providers and authority hash match per frame.");

                switch (skill)
                {
                    case "LuxQ":
                        Until(() => Count(MobaExecutionKind.DamageApply, scenario.Skill) >= 2 && ProjectileCount() == 0, 60, "two hits and no projectile");
                        Require(targets.Take(2).Select((target, index) => Hp(target) < initialHp[index] && BuffCount(target, 30070290) == 1).All(hit => hit),
                            "the first two targets must each take damage and have one root");
                        Require(Hp(targets[2]) == initialHp[2] && BuffCount(targets[2], 30070290) == 0, "the third target must not be hit");
                        for (var step = 0; step < 10; step++) Push();
                        Require(Count(MobaExecutionKind.DamageApply, scenario.Skill) == 2, "Q must settle exactly twice");
                        break;
                    case "KarthusE":
                        Require(Hp(targets[0]) < initialHp[0], "the aura must damage the target");
                        Cast();
                        Until(() => BuffCount(casterId, 30060490) == 0 && AuraCount() == 0, 30, "toggle buff and continuous shutdown");
                        var hitsAtStop = Count(MobaExecutionKind.DamageApply, scenario.Skill);
                        for (var step = 0; step < 40; step++) Push();
                        Require(Count(MobaExecutionKind.DamageApply, scenario.Skill) == hitsAtStop, "shutdown must stop later ticks");
                        Require(passiveOwners.Length == 1 && passiveOwners.SequenceEqual(PassiveOwners()), "shutdown must preserve the passive kill-mana subscription");
                        break;
                    case "KarthusR":
                        Until(() => Count(MobaExecutionKind.DamageApply, scenario.Skill) >= 2 && targets.All(target => BuffCount(target, 30060590) == 0),
                            120, "global damage and warning cleanup");
                        Require(targets.Select((target, index) => Math.Abs(initialHp[index] - Hp(target) - 200f) < 0.02f).All(hit => hit),
                            "each distant target must take exactly the configured rank-one damage");
                        for (var step = 0; step < 10; step++) Push();
                        Require(Count(MobaExecutionKind.DamageApply, scenario.Skill) == 2, "R must settle once per target");
                        break;
                    case "AnnieR":
                        var tibbers = new List<int>();
                        Require(summons.TryGetSummons(casterId, tibbers, 30030502) && tibbers.Count == 1, "R must own exactly one Tibbers");
                        var summonId = tibbers[0];
                        Require(actors.TryGet(summonId, out var summon) && summon.hasOwnerLink && summon.ownerLink.OwnerActorId == casterId && summon.hasActorBrain,
                            "Tibbers must retain its owner and brain");
                        Require(Hp(targets[0]) < initialHp[0], "R impact must damage the target");
                        Until(() => Count(MobaExecutionKind.DamageApply, 30030102, summonId) > 0, 180, "Tibbers attack");
                        Until(() => summons.GetAliveCount(casterId, 30030502) == 0 && !actors.TryGet(summonId, out _), 460, "Tibbers lifetime cleanup");
                        Require(Count(MobaExecutionKind.SummonSpawn, 30030502) == 1 && Count(MobaExecutionKind.DamageApply, scenario.Skill) == 1,
                            "R must spawn once and settle impact once");
                        break;
                }
                report($"{skill}: settlement/cleanup through {limit}; skill hits={Count(MobaExecutionKind.DamageApply, scenario.Skill)}.");

                bool CheckpointIsLive() => skill switch
                {
                    "LuxQ" => ProjectileCount() == 1 && Count(MobaExecutionKind.DamageApply, scenario.Skill) == 0,
                    "KarthusE" => BuffCount(casterId, 30060490) == 1 && AuraCount() == 1,
                    "KarthusR" => targets.All(target => BuffCount(target, 30060590) == 1) && Count(MobaExecutionKind.DamageApply, scenario.Skill) == 0,
                    "AnnieR" => summons.GetAliveCount(casterId, 30030502) == 1,
                    _ => false
                };

                void Correct(int target, int count)
                {
                    prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(target + 1), new WorldStateHash(baseline[target + 1].Hash));
                    Require(prediction.TotalRollbackRestoreFailed == 0 && prediction.TotalRollbackCount == count &&
                        prediction.LastRollbackFrame.Value == target && prediction.IsReplaying,
                        $"correction {count} did not restore {target}; restores={prediction.TotalRollbackCount}, failures={prediction.TotalRollbackRestoreFailed}");
                    Compare(target);
                    for (var frame = target + 1; frame <= liveThrough; frame++)
                    {
                        Advance(frame);
                        Compare(frame);
                    }
                    host.Tick(fixedDelta);
                    Require(!prediction.IsReplaying, "replay did not finish");
                }

                void Compare(int frame) => CompareSnapshots(baseline[frame], frame);
            }
            finally
            {
                prediction.Uninstall(host, options);
            }

            (WorldRollbackSnapshot Snapshot, uint Hash) Observe(int frame) => (observations.Capture(new FrameIndex(frame)), BattleHash());

            void CompareSnapshots((WorldRollbackSnapshot Snapshot, uint Hash) expected, int frame)
            {
                var actual = Observe(frame);
                Require(expected.Snapshot.Entries.Select(entry => entry.Key).SequenceEqual(actual.Snapshot.Entries.Select(entry => entry.Key)),
                    $"provider keys diverged at frame {frame}");
                for (var index = 0; index < actual.Snapshot.Entries.Length; index++)
                {
                    var entry = expected.Snapshot.Entries[index];
                    Require(entry.Payload.AsSpan().SequenceEqual(actual.Snapshot.Entries[index].Payload),
                        $"provider {entry.Key} diverged at frame {frame}");
                }
                Require(expected.Hash == actual.Hash, $"authority hash diverged at frame {frame}");
            }

            void CheckAnnieRExpiry()
            {
                const int tibbersConfig = 30030502;
                var events = services.Resolve<IEventBus>();
                var brains = services.Resolve<MobaBrainService>();
                var casts = ((global::Contexts)services.Resolve<global::Entitas.IContexts>()).actor.GetGroup(ActorMatcher.SkillCastInstanceId);
                var spawned = new List<SummonEventPayload>();
                var despawned = new List<SummonEventPayload>();
                using var spawnListener = events.Subscribe(SummonEventKey(MobaSummonTriggering.Events.Spawned), payload =>
                {
                    if (IsTibbers(payload)) spawned.Add(payload);
                });
                using var despawnListener = events.Subscribe(SummonEventKey(MobaSummonTriggering.Events.Despawned), payload =>
                {
                    if (IsTibbers(payload)) despawned.Add(payload);
                });

                Cast();
                Until(() => summons.GetAliveCount(casterId, tibbersConfig) == 1, 100, "Annie R spawned Tibbers");
                Require(spawned.Count == 1, "the real cast must publish exactly one summon spawn");
                var tibbersId = spawned[0].SummonActorId;
                var entity = Actor(tibbersId);
                Require(entity.hasLifetime && entity.hasActorBrain, "Tibbers must have a lifetime and brain");
                var endTime = entity.lifetime.EndTimeMs;
                var behaviorId = entity.actorBrain.BehaviorInstanceId;
                Require(brains.TryGetBehavior(behaviorId, out var originalBrain), "summon Brain must be active before expiry");
                var version = actors.GetEntityVersion(tibbersId);
                services.Resolve<MobaAuthorityFrameService>().BindRestoreFloor(world.Id,
                    new VerifiedFloorSource(prediction));

                Until(() => Remaining() <= 450, 460, "late live summon window");
                var liveCheckpoint = limit;
                CheckLive();
                var live = new[] { Observe(limit), default, default, default };
                mismatch = liveCheckpoint + 1;
                for (var index = 1; index < live.Length; index++)
                {
                    Push();
                    live[index] = Observe(limit);
                }
                mismatch = -1;
                prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(liveCheckpoint + 1), new WorldStateHash(live[1].Hash));
                Require(prediction.TotalRollbackCount == 1 && prediction.TotalRollbackRestoreFailed == 0 &&
                    prediction.LastRollbackFrame.Value == liveCheckpoint && prediction.IsReplaying, "late live correction must start reloading the original trajectory");
                CompareSnapshots(live[0], liveCheckpoint);
                CheckLive();
                for (var index = 1; index < live.Length; index++)
                {
                    Advance(liveCheckpoint + index);
                    CompareSnapshots(live[index], liveCheckpoint + index);
                    CheckLive();
                }
                host.Tick(fixedDelta);
                Require(!prediction.IsReplaying, "late live replay must finish");
                report($"Annie R late live: {liveCheckpoint}->{limit}; {live[0].Snapshot.Entries.Length} providers matched; Tibbers={tibbersId}/{version}, deadline={endTime}ms.");

                Until(() => Remaining() <= 120, 20, "pre-expiry checkpoint");
                var beforeExpiry = limit;
                CheckLive();
                var checkpoint = Observe(beforeExpiry);
                var trajectory = new Dictionary<int, (WorldRollbackSnapshot Snapshot, uint Hash)>
                {
                    [beforeExpiry] = checkpoint
                };
                mismatch = beforeExpiry + 1;
                Push();
                CheckLive();
                trajectory[limit] = Observe(limit);
                var authoritative = new WorldStateHash(BattleHash());
                mismatch = -1;
                for (var step = 0; step < 20 && actors.Contains(tibbersId); step++)
                {
                    Push();
                    trajectory[limit] = Observe(limit);
                }
                var expired = limit;
                var expiredState = trajectory[expired];
                CheckExpired(1, reclaimed: false);
                Require(!prediction.TryGetRestoreFloor(world.Id, out _), "input confirmation must not reclaim the retained Actor");
                Require(observations.TryRestore(expiredState.Snapshot, out var atExpiry), $"restore at expiry failed: {atExpiry}");
                CompareSnapshots(expiredState, expired);
                CheckExpired(1, reclaimed: false);

                prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(beforeExpiry + 1), authoritative);
                if (prediction.TotalRollbackRestoreFailed != 0)
                {
                    observations.TryRestore(checkpoint.Snapshot, out var diagnostic);
                    report($"Expiry recovery diagnostic: {diagnostic}");
                }
                Require(prediction.TotalRollbackCount == 2 && prediction.TotalRollbackRestoreFailed == 0 && prediction.IsReplaying,
                    $"expiry correction must replay the original summon; failures={prediction.TotalRollbackRestoreFailed}");
                Require(!prediction.TryCommitVerifiedRestoreAnchor(world.Id, new FrameIndex(expired)), "in-flight replay cannot commit reclaim");
                CompareSnapshots(checkpoint, beforeExpiry);
                CheckLive(1);
                for (var frame = beforeExpiry + 1; frame <= expired; frame++)
                {
                    Advance(frame);
                    CompareSnapshots(trajectory[frame], frame);
                    if (frame < expired) CheckLive(1);
                }
                host.Tick(fixedDelta);
                CheckExpired(2, reclaimed: false);
                Require(!prediction.IsReplaying, "expiry replay must finish before finalization");
                Push();
                Push();
                Require(prediction.TryGetFrames(world.Id, out var confirmed, out var predicted) &&
                    confirmed.Value > expired && predicted.Value > expired, "expiry anchor must arrive after later complete frames");
                CheckExpired(2, reclaimed: false);
                Require(prediction.TryCommitVerifiedRestoreAnchor(world.Id, new FrameIndex(expired - 1)), "older verified anchor did not commit");
                Push();
                CheckExpired(2, reclaimed: false);
                var sealedFrame = new FrameIndex(expired);
                Require(prediction.TryCommitVerifiedRestoreAnchor(world.Id, sealedFrame), "late verified expiry boundary did not commit");
                Require(prediction.TryCommitVerifiedRestoreAnchor(world.Id, sealedFrame), "duplicate anchor must be idempotent");
                Require(!prediction.TryCommitVerifiedRestoreAnchor(world.Id, new FrameIndex(expired - 1)), "old anchor cannot lower restore floor");
                observations.StoreSnapshot(expiredState.Snapshot);
                Require(observations.TryAdvanceRestoreFloor(sealedFrame), "observation coordinator boundary did not commit");
                Push();
                CheckExpired(2, reclaimed: true);
                Require(!observations.TryRestore(checkpoint.Snapshot, out var rejection) &&
                    rejection.Status == RollbackOperationStatus.BeforeRestoreFloor && rejection.ProviderCount == 0,
                    $"old direct snapshot must be rejected before import: {rejection}");
                Require(!prediction.TryRebase(world.Id, new FrameIndex(beforeExpiry)), "old prediction baseline must be rejected");
                Push();
                CheckExpired(2, reclaimed: true);
                report($"Annie R expiry replay: {beforeExpiry}->{expired}, {checkpoint.Snapshot.Entries.Length} providers matched; original Actor/Brain, silent reclaim, old snapshot imports={rejection.ProviderCount}.");

                long Remaining() => endTime - (long)MathF.Round(frameTime.Time * 1000f);

                bool IsTibbers(SummonEventPayload payload) => payload.SummonId == tibbersConfig && payload.RootOwnerActorId == casterId;
                EventKey<SummonEventPayload> SummonEventKey(string name) => new EventKey<SummonEventPayload>(TriggeringIdUtil.GetEventEid(name));

                void CheckLive(int priorTimeouts = 0)
                {
                    var current = Actor(tibbersId);
                    Require(actors.MatchesEntityVersion(tibbersId, version) && current.hasLifetime && current.lifetime.EndTimeMs == endTime &&
                        current.hasOwnerLink && current.ownerLink.OwnerActorId == casterId && current.ownerLink.RootOwnerActorId == casterId &&
                        ReferenceEquals(current, entity) && current.hasActorBrain && current.actorBrain.BehaviorInstanceId == behaviorId &&
                        brains.TryGetBehavior(behaviorId, out var currentBrain) && ReferenceEquals(currentBrain, originalBrain) &&
                        summons.GetAliveCount(casterId, tibbersConfig) == 1 && Remaining() > 0, "original live summon identity, lifetime, owner and AI must survive");
                    Require(!casts.GetEntities().Any(cast => cast.hasSkillCastOwnerActorId &&
                        (cast.skillCastOwnerActorId.Value == casterId || cast.skillCastOwnerActorId.Value == tibbersId)),
                        "late checkpoint must not contain an Annie or Tibbers Cast Entity");
                    Require(spawned.Count == 1 && despawned.Count == priorTimeouts, "live restore must not publish summon lifecycle events");
                }

                void CheckExpired(int timeoutCount, bool reclaimed)
                {
                    Require(!actors.TryGet(tibbersId, out _) && !brains.TryGetBehavior(behaviorId, out _) &&
                        summons.GetAliveCount(casterId, tibbersConfig) == 0, "expired Tibbers must remain absent from actor, brain and summon indexes");
                    Require(entity.isEnabled != reclaimed && (reclaimed || (!entity.hasActorId && entity.hasActorDespawnRequest)),
                        "physical release must happen only after the restore floor reaches expiry");
                    Require(spawned.Count == 1 && despawned.Count == timeoutCount && despawned.All(item =>
                        item.SummonActorId == tibbersId && item.Reason == (int)SummonDespawnReason.Timeout),
                        "natural timeout must publish once per simulated trajectory; rebind and reclaim are silent");
                }
            }

            void Cast()
            {
                Push(SkillInputPhase.Press);
                Push(SkillInputPhase.Release);
            }

            void Push(SkillInputPhase? phase = null)
            {
                var frame = new FrameIndex(++limit);
                var commands = Array.Empty<PlayerInputCommand>();
                if (phase.HasValue)
                {
                    sequence++;
                    var input = new SkillInputEvent(scenario.Slot, phase.Value, aimPos: in aim, predictionKey: sequence,
                        targetFrame: frame.Value, inputSequence: sequence, entityVersion: actors.GetEntityVersion(casterId));
                    commands = new[] { new PlayerInputCommand(frame, player, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input)) };
                }
                remote.Add(frame.Value, commands);
                Advance(frame.Value);
            }

            void Advance(int frame)
            {
                host.Tick(fixedDelta);
                var found = prediction.TryGetFrames(world.Id, out var confirmed, out var predicted);
                Require(found && confirmed.Value == frame && predicted.Value == frame && frameTime.Frame.Value == frame,
                    $"expected frame {frame}, reached {confirmed.Value}/{predicted.Value}/{frameTime.Frame.Value}");
            }

            void Until(Func<bool> condition, int maxFrames, string stage)
            {
                for (var step = 0; step < maxFrames && !condition(); step++) Push();
                Require(condition(), $"did not reach {stage}; skill hits={Count(MobaExecutionKind.DamageApply, scenario.Skill)}, " +
                    $"projectiles={ProjectileCount()}, E buff/continuous={BuffCount(casterId, 30060490)}/{AuraCount()}, " +
                    $"warnings={string.Join(" | ", services.Resolve<IMobaBattleDiagnosticsService>().GetWarningsSnapshot().Select(item => item.Message))}");
            }

            int Count(MobaExecutionKind kind, int configId, int sourceId = 0) => trace.GetNodesByKind((int)kind)
                .Count(node => node.Metadata?.ConfigId == configId && (sourceId == 0 || node.Metadata.SourceActorId == sourceId));

            float Hp(int id) => new MobaAttrs(Actor(id)).Hp;

            int BuffCount(int id, int buffId)
            {
                var actor = Actor(id);
                return actor.hasBuffs ? actor.buffs.Active?.Count(buff => buff != null && buff.BuffId == buffId) ?? 0 : 0;
            }

            global::ActorEntity Actor(int id) => actors.TryGet(id, out var actor) ? actor : throw new InvalidOperationException($"Missing actor {id}");

            int ProjectileCount()
            {
                var entries = new List<MobaProjectileLinkDiagnostics>();
                links.CopyDiagnosticsTo(entries);
                return entries.Count(entry => entry.Source.SourceActorId == casterId);
            }

            int SkillHits() => Count(MobaExecutionKind.DamageApply, scenario.Skill, casterId);

            int AuraCount() => continuous.GetOwnerContinuous(casterId).Count(view => view.ConfigId == 30060490);

            long[] PassiveOwners()
            {
                var keys = new List<long>();
                subscriptions.CopyOwnerKeysForTrigger(30060421, keys);
                return keys.OrderBy(key => key).ToArray();
            }

            void Require(bool condition, string detail)
            {
                if (!condition) throw new InvalidOperationException($"{skill} at frame {frameTime.Frame.Value}: {detail}");
            }
        }

        private sealed class VerifiedFloorSource : IWorldRestoreFloorSource
        {
            private readonly IClientPredictionRestoreFloor _floor;

            public VerifiedFloorSource(IClientPredictionRestoreFloor floor) => _floor = floor;

            public bool TryGetRestoreFloor(WorldId worldId, out FrameIndex frame) => _floor.TryGetRestoreFloor(worldId, out frame);
        }
    }
}
