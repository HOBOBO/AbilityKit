using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Deterministic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Pipeline;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario
{
    internal static partial class MobaStateSyncClientReplay
    {
        private static LocalStateSyncReplay CaptureEconomyTrack(bool accepted, in LocalStateSyncCastScope scope,
            int tickRate, AuthoritativeStateRecoveryRuntime recovery, Func<int, GatewayStateSyncSnapshot> createSnapshot,
            Func<int> readAppliedTick)
        {
            const int baseline = 10, firstInput = 11, anchorTick = 12, pendingTick = 14, deliveryTick = 15, nextTick = 16;
            var fixedDelta = 1f / tickRate;
            using var authority = new CastTrack(true, fixedDelta);
            var session = recovery.AttachLocalCast<CastAnchor, LocalStateSyncReplay.SkillState>(in scope,
                () => new CastTrack(true, fixedDelta));
            var initial = createSnapshot(baseline);
            var initialStamp = new LocalStateSyncCastStamp(in scope, baseline, initial.SnapshotId);
            var initialAnchor = new LocalStateSyncCastAnchor<CastAnchor, LocalStateSyncReplay.SkillState>(
                in initialStamp, authority.Capture(), authority.Observation);
            if (recovery.CommitLocalCastSnapshot(session, in initial, in initialAnchor,
                    new LocalStateSyncCastTerminalProof(in initialStamp, 1)) != LocalStateSyncCastResult.Published ||
                readAppliedTick() != baseline)
                throw new InvalidOperationException("会话未采用初始施法基线。");

            var delivery = createSnapshot(anchorTick);
            var stamp = new LocalStateSyncCastStamp(in scope, anchorTick, delivery.SnapshotId);
            LocalStateSyncCastAnchor<CastAnchor, LocalStateSyncReplay.SkillState> anchor = default;
            var checkpoints = new List<LocalStateSyncReplay.Checkpoint>();
            var steps = new List<LocalStateSyncReplay.SkillReplayStep>();
            string oldRejection = null;
            string authorityRejection = null;
            long? replayed = null;
            var lastOldPrediction = session.Observation;
            for (var tick = baseline; tick <= nextTick; tick++)
            {
                var referenceRejection = authority.Advance(tick,
                    (accepted && tick == anchorTick) || tick == pendingTick || tick == nextTick);
                if (tick == pendingTick) authorityRejection = referenceRejection;
                var sequence = tick == firstInput ? 2L : tick == pendingTick ? 3L : tick == nextTick ? 4L : 0;
                SkillInputEvent? input = sequence == 0 ? null :
                    new SkillInputEvent(1, SkillInputPhase.Press, targetFrame: tick, inputSequence: sequence);
                if (tick != baseline && (!session.Advance(tick, input) || !session.HasPrediction))
                    throw new InvalidOperationException($"会话未继续施法预测：Tick {tick}。");
                if (tick <= deliveryTick) lastOldPrediction = session.Observation;
                if (tick == anchorTick)
                    anchor = new LocalStateSyncCastAnchor<CastAnchor, LocalStateSyncReplay.SkillState>(
                        in stamp, authority.Capture(), authority.Observation);
                if (tick == pendingTick) oldRejection = session.LastInputRejection;
                if (tick == deliveryTick)
                {
                    var proof = new LocalStateSyncCastTerminalProof(in stamp, 2);
                    if (recovery.CommitLocalCastSnapshot(session, in delivery, in anchor, proof) != LocalStateSyncCastResult.Published)
                        throw new InvalidOperationException("会话未发布追平结果。", session.LastCandidateFailure);
                    if (readAppliedTick() != anchorTick || session.PublishedTick != deliveryTick || session.PendingCount != 1)
                        throw new InvalidOperationException("会话未保留正确的权威 Tick、预测 Tick 或待确认输入。");
                    foreach (var step in session.LastReplay)
                    {
                        var expected = step.Tick == tick ? authority.Observation : checkpoints[step.Tick - baseline].Skill.Authority;
                        if (!step.State.Matches(expected) || (step.Input.HasValue && step.Rejection != authorityRejection))
                            throw new InvalidOperationException($"施法和解结果与权威参考不符：Tick {step.Tick}。");
                        if (step.Input.HasValue) replayed = step.Input;
                        steps.Add(new LocalStateSyncReplay.SkillReplayStep
                        {
                            Tick = step.Tick, State = step.State, InputRejection = step.Rejection,
                        });
                    }
                    if (recovery.CommitLocalCastSnapshot(session, in delivery, in anchor, proof) != LocalStateSyncCastResult.Ignored)
                        throw new InvalidOperationException("重复截面不应再次接管预测域。");
                }
                if (tick == nextTick && (session.PublishedTick != nextTick || session.PendingCount != 2 ||
                        session.LastInputRejection != referenceRejection || !session.Observation.Matches(authority.Observation)))
                    throw new InvalidOperationException("接管后的预测域未按原状态处理下一条输入。");

                checkpoints.Add(new LocalStateSyncReplay.Checkpoint
                {
                    Command = tick == deliveryTick ? "迟到施法截面" : tick == nextTick ? "接管后继续输入" : "本地施法推进",
                    DeliveryTick = tick, BaselineTick = baseline, Generation = 1, AppliedTick = readAppliedTick(),
                    Skill = new LocalStateSyncReplay.SkillTickObservation
                    {
                        Authority = authority.Observation, OldPrediction = lastOldPrediction, Published = session.Observation,
                    },
                });
            }

            var replay = new LocalStateSyncReplay
            {
                CaseId = "moba-local-cast-window", Variant = accepted ? "economy-confirmed" : "economy-rejected",
                Version = LocalStateSyncReplay.SkillTrackVersion, TickRate = tickRate, Checkpoints = checkpoints.ToArray(),
                SkillDelivery = new LocalStateSyncReplay.SkillDeliveryObservation
                {
                    PrimaryLabel = "施法前摇", SecondaryLabel = "再次按下", FirstInputTick = firstInput,
                    AnchorTick = anchorTick, InputTick = pendingTick, DeliveryTick = deliveryTick, CastEconomy = true,
                    TerminalThroughSequence = session.TerminalThrough, PendingSequence = 3, ReplayedSequence = replayed,
                    OldInputRejectCode = oldRejection, Steps = steps.ToArray(),
                },
            };
            replay.Validate();
            return replay;
        }

        /// <summary>
        /// 为每条观测轨道隔离装配既有资源、槽冷却、经济事务和施法窗口。
        /// 仅用于本地配方；不定义线上技能截面或跨会话身份重建。
        /// </summary>
        private sealed class CastEconomyScope : IDisposable
        {
            private const int SkillId = 1;
            private const int Slot = 1;
            private readonly Contexts _entities = new Contexts();
            private readonly MobaActorRegistry _actors = new MobaActorRegistry();
            private readonly MobaEntityManager _entityManager = new MobaEntityManager(null);
            private readonly FrameTime _clock = new FrameTime();
            private readonly MobaExecutionContextRegistry _identities = new MobaExecutionContextRegistry();
            private readonly MobaSkillCastRuntimeService _runtimes = new MobaSkillCastRuntimeService();
            private readonly ActiveSkillRuntime _slot = new ActiveSkillRuntime { SkillId = SkillId, Level = 1, MaxCharges = 1, CurrentCharges = 1 };
            private readonly ResourceState _mana = new ResourceState { Current = Fixed64.FromInt32(100), LastMax = Fixed64.FromInt32(100) };
            private readonly ActorIdIndex _index;
            private readonly MobaSkillWindowRuntimeService _windows;
            private readonly MobaSkillEconomyService _economy;
            private readonly WorldContainer _services;

            internal CastEconomyScope(SkillCastPipeline pipeline)
            {
                var actor = _entities.actor.CreateEntity();
                actor.AddActorId(ActorId);
                actor.AddSkillLoadout(0, new[] { _slot }, Array.Empty<PassiveSkillRuntime>());
                actor.AddResourceContainer(new ResourceContainer
                {
                    Map = new Dictionary<ResourceType, ResourceState> { [ResourceType.Mana] = _mana },
                }, true);
                _actors.Register(ActorId, actor);
                _index = new ActorIdIndex(_entities);
                var lookup = new MobaActorLookupService(_index, _actors, _entityManager, _entities);
                _economy = new MobaSkillEconomyService(_runtimes, lookup, _clock);
                _windows = new MobaSkillWindowRuntimeService(_runtimes);
                _services = new WorldContainerBuilder()
                    .RegisterExternalInstance<IFrameTime>(_clock)
                    .RegisterExternalInstance(_identities)
                    .RegisterExternalInstance(_runtimes)
                    .RegisterExternalInstance(_windows)
                    .RegisterExternalInstance(_economy)
                    .Build();
                pipeline.AddPhase(new AbilityActionPhase<SkillPipelineContext>(new AbilityPipelinePhaseId("reserve"), context =>
                {
                    var specification = new SkillEconomyPhaseDTO
                    {
                        ResourceType = (int)ResourceType.Mana, ResourceAmount = 10, UseResolvedResourceCost = false,
                        RequireExplicitCommit = true, StartSkillCooldown = true, UseResolvedSkillCooldown = false,
                        SkillCooldownMs = 500,
                    };
                    if (!_economy.TryReserve(context, specification, out var failure) || !_economy.Commit(context.RuntimeHandle))
                        throw new InvalidOperationException($"本地配方技能经济提交失败：{failure}");
                }));
                pipeline.AddPhase(new SkillWindowPhase(new AbilityPipelinePhaseId("windup"), new SkillWindowPhaseDTO
                {
                    WindowId = "scenario-economy-windup", Kind = (int)SkillWindowKind.Timed, DurationMs = 400,
                }, null));
            }

            internal void Align(int tick, float fixedDelta)
            {
                if (!_clock.AlignTo(new FrameIndex(tick), fixedDelta))
                    throw new InvalidOperationException("本地施法经济时钟未对齐。");
            }

            internal string ReadInputRejection()
            {
                var availability = _economy.GetAvailability(ActorId, _slot, 0, string.Empty, false);
                return availability.Available ? null : availability.Reason;
            }

            internal SkillPipelineContext CreateContext()
            {
                var pos = Vec3.Zero;
                var dir = Vec3.Forward;
                var tick = _clock.Frame.Value;
                var root = _identities.Create(new MobaExecutionContextCreateRequest(
                    MobaExecutionKind.SkillCast, SkillId, ActorId, 0, frame: tick));
                var runtime = _runtimes.Create(new MobaSkillCastRuntimeCreateRequest(
                    SkillId, Slot, 1, tick, ActorId, 0, in pos, in dir, root.ContextId));
                return BindContext(runtime.Handle, root.ContextId, in pos, in dir);
            }

            internal LocalStateSyncReplay.SkillState Observe(SkillPipelineContext context)
            {
                var state = new LocalStateSyncReplay.SkillState
                {
                    ManaRaw = _mana.Current.RawValue,
                    PrimaryCooldownEndMs = _slot.CooldownEndTimeMs,
                };
                if (context != null)
                {
                    var handle = context.RuntimeHandle;
                    if (!_windows.TryGetSnapshot(in handle, out var window))
                        throw new InvalidOperationException("本地配方的活动施法缺少窗口观测。");
                    state.CastElapsedMs = window.ElapsedMs;
                }
                return state;
            }

            internal EconomyAnchor Capture(SkillPipelineContext context) => new EconomyAnchor
            {
                Mana = _mana.Current, CooldownEnd = _slot.CooldownEndTimeMs, CooldownDuration = _slot.CooldownDurationMs,
                Runtimes = _runtimes.CaptureRollbackSnapshot(), Economy = _economy.CaptureRollbackSnapshot(),
                Identities = _identities.CaptureLifecycleSnapshot(),
                Context = context?.CaptureRollbackState(), Handle = context?.RuntimeHandle ?? default,
                SourceContextId = context?.SourceContextId ?? 0,
            };

            internal SkillPipelineContext Restore(EconomyAnchor anchor)
            {
                /*
                 * 已确认分支须先装配同一身份骨架，生命周期恢复只更新已有身份。
                 */
                if (anchor.Context.HasValue)
                {
                    if (anchor.Identities.Count != 1)
                        throw new InvalidOperationException("本地施法截面必须只有一个根身份。");
                    _identities.Create(new MobaExecutionContextCreateRequest(MobaExecutionKind.SkillCast,
                        SkillId, ActorId, 0, frame: anchor.Identities[0].CreatedFrame));
                }
                _identities.RestoreLifecycle(anchor.Identities);
                _runtimes.RestoreRollbackSnapshot(in anchor.Runtimes);
                _economy.RestoreRollbackSnapshot(in anchor.Economy);
                _mana.Current = anchor.Mana;
                _slot.CooldownEndTimeMs = anchor.CooldownEnd;
                _slot.CooldownDurationMs = anchor.CooldownDuration;
                if (anchor.Context is not { } state) return null;
                var pos = state.AimPos;
                var dir = state.AimDir;
                var context = BindContext(anchor.Handle, anchor.SourceContextId, in pos, in dir);
                context.RestoreRollbackState(in state);
                return context;
            }

            private SkillPipelineContext BindContext(MobaSkillCastRuntimeHandle handle, long source, in Vec3 pos, in Vec3 dir)
            {
                var request = new SkillCastRequest(SkillId, Slot, ActorId, 0, in pos, in dir, _services, null, null, null);
                var trigger = new SkillCastContext();
                trigger.Initialize(in request, 1);
                trigger.RuntimeHandle = handle;
                trigger.RuntimeId = handle.RuntimeId;
                trigger.SourceContextId = source;
                var context = new SkillPipelineContext();
                context.Initialize(null, in request, trigger);
                return context;
            }

            public void Dispose()
            {
                _services.Dispose();
                _economy.Dispose();
                _runtimes.Dispose();
                _identities.Dispose();
                _index.Dispose();
                _entities.actor.DestroyAllEntities();
                _actors.Dispose();
                _entityManager.Dispose();
            }
        }

        /// <summary>
        /// 本地预览的类型化资源截面，与实际投递的角色快照分开。
        /// </summary>
        private sealed class EconomyAnchor
        {
            internal Fixed64 Mana;
            internal long CooldownEnd;
            internal int CooldownDuration;
            internal MobaSkillCastRuntimeServiceSnapshot Runtimes;
            internal MobaSkillEconomyServiceSnapshot Economy;
            internal IReadOnlyList<MobaExecutionContextNode> Identities;
            internal SkillPipelineContextRollbackState? Context;
            internal MobaSkillCastRuntimeHandle Handle;
            internal long SourceContextId;
        }
    }
}
