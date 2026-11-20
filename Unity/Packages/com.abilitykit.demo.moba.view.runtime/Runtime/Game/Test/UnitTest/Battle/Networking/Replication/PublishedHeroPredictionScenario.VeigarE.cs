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
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Trace;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Game.Test.UnitTest
{
    internal static partial class PublishedHeroPredictionScenario
    {
        private static void CheckEventHorizon(IWorld world, IWorldManager worlds, PlayerId caster, int casterId,
            int targetId, float fixedDelta, Action<string> report)
        {
            const int edgeTemplate = 42040401;
            const int stunBuff = 32040401;
            const int stunEffect = 30040413;
            var scenario = Describe("VeigarE");
            var aim = scenario.Aim;
            var mover = new PlayerId(TargetPlayerName(0));
            var services = world.Services;
            var actors = services.Resolve<MobaActorRegistry>();
            var gate = services.Resolve<MobaLogicWorldRunGateService>();
            var frameTime = services.Resolve<IFrameTime>();
            var trace = services.Resolve<MobaTraceRegistry>();
            var areaService = services.Resolve<MobaAreaRuntimeService>();
            var gates = services.Resolve<IMobaTagBehaviorGateQueryService>();
            var casts = ((global::Contexts)services.Resolve<global::Entitas.IContexts>()).actor.GetGroup(ActorMatcher.SkillCastInstanceId);
            var edges = new List<MobaAreaRuntimeInfo>(6);
            var crossings = new List<(int AreaId, int Frame, int Direction)>();
            using var listener = services.Resolve<IEventBus>().Subscribe(
                new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid("area.cross")), info =>
                {
                    if (info.TemplateId == edgeTemplate && info.OwnerActorId == casterId && info.TargetActorId == targetId)
                        crossings.Add((info.AreaId, info.Frame, info.CrossingDirection));
                });
            var calculator = new MobaAuthoritativeStateHashCalculator();
            var observations = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(world), new RollbackSnapshotRingBuffer(4));
            using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
            var options = new HostRuntimeOptions();
            var host = new HostRuntime(worlds, options);
            var limit = frameTime.Frame.Value;
            var mismatch = -1;
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
                Require(services.Resolve<MobaPlayerActorMapService>().TryGetActorId(mover, out var mapped) && mapped == targetId,
                    "movement player must be bound to the enemy actor");
                PushSkill(SkillInputPhase.Press, 1);
                PushSkill(SkillInputPhase.Release, 2);
                Until(() => ReadEdges() && edges.Count == 6 && edges.All(edge => edge.DelayTriggerFrame <= limit), 65, "all six armed cage edges");
                Require(edges.All(edge => edge.ClaimTargetsWithinGroup) && edges.Select(edge => (edge.RootContextId, edge.GroupKey)).Distinct().Count() == 1,
                    "all six edges must share the claim group");
                var edgeIds = edges.Select(edge => edge.AreaId).OrderBy(id => id).ToArray();
                Require(TargetX() > aim.X + 4 && Stuns() == 0 && crossings.Count == 0, "the target must start outside with no stun");

                var inbound = limit;
                var baseline = StartLeg();
                for (var step = 0; step < 35 && Stuns() == 0; step++) MoveAndCapture(-1f, baseline);
                MoveAndCapture(0f, baseline);
                Require(crossings.Count == 1 && crossings[0].Direction == -1 && Stuns() == 1 && StunEffects() == 1,
                    $"inward crossing must apply one stun: crossings={crossings.Count}, buffs={Stuns()}, effects={StunEffects()}, x={TargetX()}");
                Require(InsideCage(), $"the swept movement must reach the cage interior, x={TargetX()}");
                CheckStunned(true);
                var expectedCross = crossings.Single();
                var firstEnd = limit;
                crossings.Clear();
                Correct(inbound, 1, baseline, () =>
                {
                    Require(crossings.Count == 0 && Stuns() == 0 && StunEffects() == 0,
                        "restore must neither fire the crossing nor retain future hits");
                    CheckStunned(false);
                });
                Require(crossings.Count == 1 && crossings[0] == expectedCross && Stuns() == 1 && StunEffects() == 1,
                    "replay must reproduce the same crossing and exactly one stun");
                CheckStunned(true);

                Until(() => BuffCount() == 0, 65, "stun expiration while the cage is still active");
                CheckStunned(false);
                Require(InsideCage() && SameCage(), $"target must remain inside the same live cage, x={TargetX()}");

                var outbound = limit;
                var castAtOutbound = CastPresent();
                baseline = StartLeg();
                for (var step = 0; step < 45 && TargetX() < aim.X + 4; step++) MoveAndCapture(1f, baseline);
                MoveAndCapture(0f, baseline);
                CheckNoSecondStun();
                var secondEnd = limit;
                Correct(outbound, 2, baseline, () =>
                {
                    Require(InsideCage() && SameCage() && Stuns() == 1 && crossings.Count == 1 && CastPresent() == castAtOutbound,
                        "post-claim checkpoint must restore the interior with the same live cage and Cast Entity presence");
                    CheckStunned(false);
                });
                CheckNoSecondStun();
                Until(() => !ReadEdges(), 65, "natural cage cleanup");
                CheckStunned(false);
                report($"VeigarE: inbound {inbound}->{firstEnd}, crossing at {expectedCross.Frame}, outbound {outbound}->{secondEnd}; " +
                    $"{baseline[outbound].Snapshot.Entries.Length} providers matched per frame, stun applications={Stuns()}, " +
                    $"cast present at outbound checkpoint={castAtOutbound}, cleanup={limit}.");

                bool SameCage() => ReadEdges() && edgeIds.SequenceEqual(edges.Select(edge => edge.AreaId).OrderBy(id => id));
                bool InsideCage() => Math.Abs(TargetX() - aim.X) <= 2.5f;

                void CheckNoSecondStun()
                {
                    Require(TargetX() >= aim.X + 4 && SameCage(), $"target must cross to the outside before the cage expires, x={TargetX()}");
                    Require(Stuns() == 1 && StunEffects() == 1 && crossings.Count == 1, "already claimed target must not trigger the cage again");
                    CheckStunned(false);
                }
            }
            finally
            {
                prediction.Uninstall(host, options);
            }

            void PushSkill(SkillInputPhase phase, int sequence)
            {
                var frame = new FrameIndex(limit + 1);
                var input = new SkillInputEvent(scenario.Slot, phase, aimPos: in aim, predictionKey: sequence,
                    targetFrame: frame.Value, inputSequence: sequence, entityVersion: actors.GetEntityVersion(casterId));
                Push(new PlayerInputCommand(frame, caster, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input)));
            }

            void Move(float direction) => Push(new PlayerInputCommand(new FrameIndex(limit + 1), mover,
                MobaOpCodes.Input.Move, MobaMoveCodec.Serialize(direction, 0f)));

            void Push(params PlayerInputCommand[] commands)
            {
                remote.Add(++limit, commands);
                Advance(limit);
            }

            void Advance(int frame)
            {
                host.Tick(fixedDelta);
                var found = prediction.TryGetFrames(world.Id, out var confirmed, out var predicted);
                Require(found && confirmed.Value == frame && predicted.Value == frame && frameTime.Frame.Value == frame,
                    $"expected frame {frame}, reached {confirmed.Value}/{predicted.Value}/{frameTime.Frame.Value}");
            }

            void Until(Func<bool> done, int maxFrames, string stage)
            {
                for (var index = 0; index < maxFrames && !done(); index++)
                    Push();
                Require(done(), $"did not reach {stage}; x={TargetX()}, edges={edges.Count}, stun count={Stuns()}, cast present={CastPresent()}");
            }

            Dictionary<int, (WorldRollbackSnapshot Snapshot, uint Hash)> StartLeg()
            {
                mismatch = limit + 1;
                return new Dictionary<int, (WorldRollbackSnapshot Snapshot, uint Hash)> { [limit] = Observe(limit) };
            }

            void MoveAndCapture(float direction, Dictionary<int, (WorldRollbackSnapshot Snapshot, uint Hash)> frames)
            {
                Move(direction);
                frames.Add(limit, Observe(limit));
            }

            (WorldRollbackSnapshot Snapshot, uint Hash) Observe(int frame) => (observations.Capture(new FrameIndex(frame)), BattleHash());

            void Correct(int target, int count, Dictionary<int, (WorldRollbackSnapshot Snapshot, uint Hash)> baseline, Action restored)
            {
                mismatch = -1;
                prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(target + 1), new WorldStateHash(baseline[target + 1].Hash));
                Require(prediction.TotalRollbackRestoreFailed == 0 && prediction.TotalRollbackCount == count &&
                    prediction.LastRollbackFrame.Value == target && prediction.IsReplaying,
                    $"correction {count} did not restore {target}; restores={prediction.TotalRollbackCount}, failures={prediction.TotalRollbackRestoreFailed}");
                Compare(target);
                restored();
                for (var frame = target + 1; frame <= limit; frame++)
                {
                    Advance(frame);
                    Compare(frame);
                }
                host.Tick(fixedDelta);
                Require(!prediction.IsReplaying, "replay did not finish");

                void Compare(int frame)
                {
                    var expected = baseline[frame];
                    var actual = Observe(frame);
                    Require(expected.Snapshot.Entries.Select(entry => entry.Key).SequenceEqual(actual.Snapshot.Entries.Select(entry => entry.Key)),
                        $"provider keys diverged at frame {frame}");
                    for (var index = 0; index < actual.Snapshot.Entries.Length; index++)
                    {
                        var entry = expected.Snapshot.Entries[index];
                        Require(entry.Payload.AsSpan().SequenceEqual(actual.Snapshot.Entries[index].Payload), $"provider {entry.Key} diverged at frame {frame}");
                    }
                    Require(expected.Hash == actual.Hash, $"authority hash diverged at frame {frame}");
                }
            }

            bool CastPresent() => casts.GetEntities().Any(entity => entity.hasSkillCastOwnerActorId &&
                entity.skillCastOwnerActorId.Value == casterId && entity.hasSkillCastSkillId && entity.skillCastSkillId.Value == scenario.Skill);
            bool ReadEdges() => areaService.TryGetAreas(edges, casterId, edgeTemplate);
            int Stuns() => trace.GetNodesByKind((int)MobaExecutionKind.BuffApply).Count(node => node.Metadata?.ConfigId == stunBuff);
            int StunEffects() => trace.GetNodesByKind((int)MobaExecutionKind.EffectExecution).Count(node => node.Metadata?.ConfigId == stunEffect);
            global::ActorEntity Target() => actors.TryGet(targetId, out var actor) ? actor : throw new InvalidOperationException($"Missing actor {targetId}");
            float TargetX() => Target().transform.Value.Position.X;
            int BuffCount() => Target().hasBuffs ? Target().buffs.Active?.Count(buff => buff != null && buff.BuffId == stunBuff) ?? 0 : 0;

            void CheckStunned(bool expected)
            {
                Require(gates.TryIsBlocked(targetId, moba_luban.MobaTagBehaviorGate.Move, out var moveBlocked) && moveBlocked == expected,
                    $"target move gate should be {expected}");
                Require(gates.TryIsBlocked(targetId, moba_luban.MobaTagBehaviorGate.Cast, out var castBlocked) && castBlocked == expected,
                    $"target cast gate should be {expected}");
                Require(BuffCount() == (expected ? 1 : 0), "target stun buff differs from expected gate state");
            }

            void Require(bool condition, string detail)
            {
                if (!condition) throw new InvalidOperationException($"VeigarE at frame {frameTime.Frame.Value}: {detail}");
            }
        }
    }
}
