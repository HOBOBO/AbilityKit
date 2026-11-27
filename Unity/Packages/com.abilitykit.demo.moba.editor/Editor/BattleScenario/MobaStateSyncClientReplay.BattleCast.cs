using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario
{
    internal static partial class MobaStateSyncClientReplay
    {
        private const int BattleCastTickRate = 32;
        private const int BattleCastHero = 3003;
        private const int BattleCastFirstSlot = 2;
        private const int BattleCastSecondSlot = 3;
        private const int BattleFlightMask = 1 << 24;
        private const int BattleFlightTemplate = 982103;

        /// <summary>
        /// 真实权威世界出截面，正式客户端提交后在隔离世界追平；两条显示轨分别读取提交与发布结果。
        /// </summary>
        private static LocalStateSyncReplay CaptureBattleFlightTrack(LocalStateSyncReplay.SkillReplayCase recipe,
            Guid stream, Guid inputGeneration, AuthoritativeStateRecoveryRuntime recovery, Func<int> readAppliedTick,
            Func<int, ActorProjectionData> readProjection)
        {
            const int firstTick = 1, anchorTick = 3, pendingTick = 5, deliveryTick = 7, endTick = 20;
            const float delta = 1f / BattleCastTickRate;
            var replayShot = recipe == LocalStateSyncReplay.SkillReplayCase.BattleFlightReplay;
            var restoreShot = recipe == LocalStateSyncReplay.SkillReplayCase.BattleFlightRestore;
            var skillIds = new[] { 30030201, 30030301, 30030401 };
            using var authority = CreateFlightWorld("scenario-flight-reference");
            var services = authority.Services;
            var clock = (FrameTime)services.Resolve<IFrameTime>();
            var registry = services.Resolve<MobaActorRegistry>();
            var cast = services.Resolve<SkillCastCoordinator>();
            var ids = registry.CopyActorIdsInOrder();
            if (ids.Count != 1 || !registry.TryGet(ids[0], out var owner))
                throw new InvalidOperationException("逻辑弹体验收缺少单一施法者。");
            var ownerId = ids[0];
            var scope = new LocalStateSyncCastScope(recovery.LocalReplayGeneration, 1, stream, inputGeneration,
                1, ownerId, registry.GetEntityVersion(ownerId));
            var candidateCount = 0;
            var projectedActorId = 0;
            var session = recovery.AttachLocalBattleCast(in scope, delta, () =>
                CreateFlightWorld($"scenario-flight-candidate-{++candidateCount}"), projectileMask: BattleFlightMask, observeActorPose: true);
            var initial = Capture(0, 1);
            var committedFlight = Commit(initial, 0);
            var anchor = initial;
            var first = Press(firstTick, 1, !replayShot, localOffset: restoreShot ? 2 : 0);
            var pending = Press(pendingTick, 2, replayShot);
            var checkpoints = new List<LocalStateSyncReplay.Checkpoint>(endTick + 1);
            var reference = new LocalStateSyncBattleCastObservation[endTick + 1];
            var steps = new List<LocalStateSyncReplay.SkillReplayStep>();
            long? replayed = null;
            for (var tick = 0; tick <= endTick; tick++)
            {
                if (tick > 0)
                {
                    clock.StepTo(new FrameIndex(tick), delta);
                    SkillInputEvent? original = tick == firstTick && (restoreShot || replayShot) ? Press(tick, 1, restoreShot) :
                        tick == pendingTick ? pending : (SkillInputEvent?)null;
                    if (original is { } input && !cast.TryHandleInputResult(ownerId, in input).Success)
                        throw new InvalidOperationException($"权威参考拒绝配方输入：Tick {tick}。");
                    authority.Tick(delta);
                    if (!session.Advance(tick, tick == firstTick ? first : tick == pendingTick ? pending : (SkillInputEvent?)null) ||
                        !session.HasPrediction || session.LastInputRejection != null)
                        throw new InvalidOperationException($"弹体预测未推进到 Tick {tick}。", session.LastCandidateFailure);
                }

                var before = session.Observation;
                reference[tick] = ReadAuthority(tick);
                if (tick == anchorTick) anchor = Capture(tick, 2);
                if (tick == deliveryTick)
                {
                    committedFlight = Commit(anchor, 1);
                    if (candidateCount != 2 || session.PublishedTick != tick || session.PendingCount != 1 ||
                        session.TerminalThrough != 1 || session.LastReplay.Count != deliveryTick - anchorTick)
                        throw new InvalidOperationException("弹体候选未完成追平或输入终结不正确。");
                    foreach (var step in session.LastReplay)
                    {
                        if (!step.State.Equals(reference[step.Tick]) || step.Rejection != null ||
                            step.Input != (step.Tick == pendingTick ? 2L : (long?)null))
                            throw new InvalidOperationException($"弹体候选 Tick {step.Tick} 与独立权威参考不符。");
                        if (step.Input.HasValue) replayed = step.Input;
                        steps.Add(new LocalStateSyncReplay.SkillReplayStep { Tick = step.Tick, State = State(step.State) });
                    }
                }

                if (tick >= deliveryTick && (!session.Observation.Equals(reference[tick]) || candidateCount != 2))
                    throw new InvalidOperationException($"追平后没有在同一候选继续推进 Tick {tick}。");
                if (tick == endTick)
                {
                    if (session.Observation.Flight != null)
                        throw new InvalidOperationException("弹体应在后续权威交付前已自然退场。");
                    committedFlight = Commit(Capture(tick, 3), 2);
                }
                var applied = readAppliedTick();
                if (applied != (tick == endTick ? endTick : tick < deliveryTick ? 0 : anchorTick))
                    throw new InvalidOperationException("权威投影不得跟随候选时钟提前推进。");

                checkpoints.Add(new LocalStateSyncReplay.Checkpoint
                {
                    Command = tick == deliveryTick ? "交付迟到的权威弹体截面" : tick == endTick ? "交付权威空名单" : "推进正式战斗 Tick",
                    DeliveryTick = tick, BaselineTick = 0, Generation = scope.Generation, AppliedTick = applied,
                    CommittedFlight = committedFlight, AuthorityPose = ToPose(readProjection(ownerId)),
                    Skill = new LocalStateSyncReplay.SkillTickObservation
                    {
                        Authority = State(reference[tick]), OldPrediction = State(before), Published = State(session.Observation),
                    },
                });
            }

            var replay = new LocalStateSyncReplay
            {
                CaseId = "moba-local-formal-flight", Variant = restoreShot ? "restore" : replayShot ? "replay" : "reject",
                Version = LocalStateSyncReplay.SkillTrackVersion, TickRate = BattleCastTickRate, Checkpoints = checkpoints.ToArray(),
                ActorModelId = BattleCastHero,
                SkillDelivery = new LocalStateSyncReplay.SkillDeliveryObservation
                {
                    PrimaryLabel = "第一输入", SecondaryLabel = replayShot ? "发射" : "空动作",
                    CastEconomy = true, FormalBattleCandidate = true, FormalFlight = true,
                    FirstInputTick = firstTick, AnchorTick = anchorTick, InputTick = pendingTick, DeliveryTick = deliveryTick,
                    TerminalThroughSequence = 1, PendingSequence = 2, ReplayedSequence = replayed, Steps = steps.ToArray(),
                },
            };
            replay.Validate();
            return replay;

            EntitasWorld CreateFlightWorld(string name)
            {
                var world = CreateBattleCastWorld(name, false, false, BattleCastHero, 30030101, skillIds);
                try { InstallFlightRecipe(world); return world; }
                catch { world.Dispose(); throw; }
            }

            SkillInputEvent Press(int tick, long sequence, bool shoot, int localOffset = 0) =>
                new SkillInputEvent(shoot ? BattleCastFirstSlot : BattleCastSecondSlot, SkillInputPhase.Press,
                    aimPos: new Vec3(0, 0, 1 + localOffset), aimDir: Vec3.Forward, targetFrame: tick, inputSequence: sequence);

            LocalStateSyncBattleCastObservation ReadAuthority(int tick)
            {
                var flight = LocalStateSyncProjectileCut.ReadFlight(authority, ownerId, BattleFlightMask, owner, out var projectile);
                var economy = new LocalStateSyncCastEconomy(owner.resourceContainer.Value.Map[ResourceType.Mana].Current.RawValue,
                    ReadSlot(BattleCastFirstSlot), ReadSlot(BattleCastSecondSlot));
                return new LocalStateSyncBattleCastObservation(tick, economy, owner.transform.Value.Position,
                    rotation: owner.transform.Value.Rotation, flight:
                    projectile == null ? (LocalStateSyncFlightObservation?)null : new LocalStateSyncFlightObservation(flight,
                        projectile.actorId.Value, registry.GetEntityVersion(projectile.actorId.Value), projectile.transform.Value));
            }

            LocalStateSyncCastSlot ReadSlot(int slot)
            {
                var value = owner.skillLoadout.ActiveSkills[slot - 1];
                return new LocalStateSyncCastSlot(slot, value.SkillId, value.Level, value.CooldownEndTimeMs, value.CooldownDurationMs);
            }

            (GatewayStateSyncSnapshot Snapshot, LocalStateSyncProjectileCut Cut, LocalStateSyncBattleCastObservation Reference) Capture(int tick, long revision)
            {
                var stamp = new LocalStateSyncCastStamp(in scope, tick, revision);
                var observation = ReadAuthority(tick);
                var position = observation.Position;
                var hp = owner.resourceContainer.Value.Map[ResourceType.Hp];
                var samples = new List<GatewayStateSyncActorSnapshot>(2)
                {
                    new GatewayStateSyncActorSnapshot(ownerId, position.X, position.Y, position.Z, 0, 0, 0,
                        hp.Current.ToSingle(), hp.LastMax.ToSingle(), (int)owner.team.Value,
                        (int)SpawnEntityKind.Character, BattleCastHero, 1),
                };
                if (observation.Flight is { } flight)
                {
                    var pose = flight.Pose;
                    samples.Add(new GatewayStateSyncActorSnapshot(flight.ActorId, pose.Position.X, pose.Position.Y, pose.Position.Z,
                        (float)Math.Atan2(pose.Forward.X, pose.Forward.Z), 0, 0, 0, 0, (int)owner.team.Value,
                        (int)SpawnEntityKind.Projectile, BattleFlightTemplate, ownerId));
                }
                return (new GatewayStateSyncSnapshot(1, tick, 0d, true, samples.ToArray(), ActorOnlySnapshotSchema,
                    eventEpoch: "scenario-epoch", usesFixedBaseline: true, stateStreamGeneration: stream, snapshotId: revision),
                    LocalStateSyncProjectileCut.Capture(authority, in stamp, BattleFlightMask), observation);
            }

            LocalStateSyncReplay.FlightSample Commit(
                (GatewayStateSyncSnapshot Snapshot, LocalStateSyncProjectileCut Cut, LocalStateSyncBattleCastObservation Reference) packet,
                long through)
            {
                var stamp = new LocalStateSyncCastStamp(in scope, packet.Snapshot.Frame, packet.Snapshot.SnapshotId);
                var coverage = LocalStateSyncBattleCastCoverage.Settled(in stamp);
                var proof = new LocalStateSyncCastTerminalProof(in stamp, through);
                var economy = packet.Reference.Economy;
                var result = recovery.CommitLocalBattleCastSnapshot(session, in packet.Snapshot, in stamp, in economy, proof,
                    in coverage, projectile: packet.Cut);
                if (result != LocalStateSyncCastResult.Published)
                    throw new InvalidOperationException($"提交权威弹体 Tick {stamp.Tick} 未发布候选：{result}。", session.LastCandidateFailure);
                if (packet.Reference.Flight is not { } flight)
                {
                    if (projectedActorId != 0 && readProjection(projectedActorId).ActorId != 0)
                        throw new InvalidOperationException("已提交的权威空名单没有移除弹体投影。");
                    projectedActorId = 0;
                    return null;
                }
                var projection = readProjection(flight.ActorId);
                var position = new Vec3(projection.PosX, projection.PosY, projection.PosZ);
                if (projection.ActorId != flight.ActorId || !position.Equals(flight.Pose.Position))
                    throw new InvalidOperationException("导入后的权威弹体投影与已提交截面不符。");
                projectedActorId = projection.ActorId;
                return Sample(flight, position);
            }

            LocalStateSyncReplay.SkillState State(LocalStateSyncBattleCastObservation observation)
            {
                var state = ToSkillState(observation, true);
                state.Flight = observation.Flight is { } flight ? Sample(flight, flight.Pose.Position) : null;
                return state;
            }
        }

        private static LocalStateSyncReplay.FlightSample Sample(LocalStateSyncFlightObservation flight, Vec3 position) =>
            new LocalStateSyncReplay.FlightSample
            {
                ProjectileId = flight.ProjectileId, ActorId = flight.ActorId, EntityVersion = flight.EntityVersion,
                LifeLeft = flight.LifetimeFramesLeft, DistanceLeftRaw = flight.DistanceLeftRaw,
                X = (int)Math.Round(position.X * 1000), Y = (int)Math.Round(position.Y * 1000), Z = (int)Math.Round(position.Z * 1000),
            };

        /// <summary>
        /// 只在离线配方安装一个直射动作和一个空动作，仍走正式施法、发射、飞行与退场系统。
        /// </summary>
        private static void InstallFlightRecipe(EntitasWorld world)
        {
            const int shotFlow = 982101, triggerId = 982102, actionId = 982104, emptyFlow = 982105, life = 12, speed = 11;
            var services = world.Services;
            var registry = services.Resolve<MobaActorRegistry>();
            var ids = registry.CopyActorIdsInOrder();
            if (ids.Count != 1 || !registry.TryGet(ids[0], out var actor))
                throw new InvalidOperationException("直线弹体配方须且仅须一名施法者。");
            var skills = actor.skillLoadout.ActiveSkills;
            var reload = services.Resolve<MobaConfigDatabase>().ReloadFromDtoArrays(new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[] { MakeSkill(BattleCastFirstSlot, shotFlow), MakeSkill(BattleCastSecondSlot, emptyFlow) },
                [typeof(ProjectileDTO)] = new[] { new ProjectileDTO { Id = BattleFlightTemplate, Speed = speed, LifetimeMs = 375,
                    MaxDistance = 40, CollisionLayerMask = BattleFlightMask, HitPolicyKind = (int)ProjectileHitPolicyKind.ExitOnHit } },
                [typeof(SkillFlowDTO)] = new[]
                {
                    new SkillFlowDTO { Id = shotFlow, Phases = new[] { new SkillPhaseDTO { Type = (int)SkillPhaseType.RulePlan,
                        PhaseId = "flight", RulePlan = new SkillRulePlanPhaseDTO { TriggerIds = new[] { triggerId }, AbortOnFailure = true } } } },
                    new SkillFlowDTO { Id = emptyFlow, Phases = new[] { new SkillPhaseDTO { Type = (int)SkillPhaseType.Delay,
                        PhaseId = "empty", Delay = new SkillDelayPhaseDTO { DelayMs = 1 } } } },
                },
            }, strict: false);
            if (!reload.Succeeded) throw new InvalidOperationException("无法配置本地逻辑弹体配方。");
            services.Resolve<TriggerPlanJsonDatabase>().LoadFromJson(@"{""format"":""runtime"",""formatVersion"":1,""triggers"":[
                {""triggerId"":982102,""actions"":[{""actionId"":982104,""arity"":0}]}]}");
            services.Resolve<ActionRegistry>().Register<NamedAction0<object, object, IWorldResolver>>(new ActionId(actionId),
                (args, _, action) =>
                {
                    if (!(args is SkillPipelineContext casts) || !casts.TryGetOrigin(out var origin))
                    {
                        action.Control.RejectAction("离线弹体配方缺少正式施法来源。");
                        return;
                    }
                    var source = new ProjectileSourceContext(casts.CasterActorId, 0, BattleFlightTemplate, origin.ImmediateContextId,
                        origin.EffectiveRootContextId, origin.OwnerContextId, casts.RuntimeHandle, in origin);
                    if (!action.Context.Resolve<MobaProjectileService>().Shoot(casts.CasterActorId, ProjectileEmitterType.Linear,
                            BattleFlightTemplate, speed, life, 40, casts.AimPos, casts.AimDir, in source))
                        action.Control.RejectAction("正式弹体发射入口拒绝离线配方。");
                }, isDeterministic: true);

            SkillDTO MakeSkill(int slot, int flow) => new SkillDTO { Id = skills[slot - 1].SkillId, Name = "离线逻辑弹体",
                CastFlowId = flow, AllowCastWithoutTarget = true };
        }

        /// <summary>
        /// 用同一正式战斗装配生成独立权威参考与恢复候选，预览只记录值，不推进快照投影世界。
        /// </summary>
        private static LocalStateSyncReplay CaptureBattleCastTrack(bool confirmed, bool windup, bool movement,
            bool rejectBeforeRelease, Guid stream, Guid inputGeneration,
            AuthoritativeStateRecoveryRuntime recovery, Func<int> readAppliedTick,
            Func<int, LocalStateSyncReplay.ActorPose> readAuthorityProjection)
        {
            var firstInput = movement ? 2 : 1;
            var authorityWindupInputTick = firstInput + 1;
            var anchorTick = rejectBeforeRelease ? 3 : windup ? authorityWindupInputTick + 1 : 100;
            var pendingTick = rejectBeforeRelease ? 5 : movement ? 6 : windup ? 0 : 102;
            var deliveryTick = rejectBeforeRelease ? 7 : windup ? 9 : 106;
            var nextTick = deliveryTick + 1;
            var heroId = rejectBeforeRelease ? 3007 : BattleCastHero;
            var basicAttack = rejectBeforeRelease ? 30070101 : 30030101;
            var skillIds = rejectBeforeRelease ? new[] { 30070201, 30070301, 30070401, 30070501 } :
                new[] { 30030201, 30030301, 30030401 };
            var firstSlot = rejectBeforeRelease ? 1 : BattleCastFirstSlot;
            var secondSlot = rejectBeforeRelease ? 2 : BattleCastSecondSlot;
            var observeCastProgress = windup || rejectBeforeRelease;
            const int releaseAtMs = 250;
            const float delta = 1f / BattleCastTickRate;
            using var authority = CreateBattleCastWorld("scenario-cast-reference", windup, movement, heroId, basicAttack, skillIds);
            var services = authority.Services;
            var frame = (FrameTime)services.Resolve<IFrameTime>();
            var registry = services.Resolve<MobaActorRegistry>();
            var cast = services.Resolve<SkillCastCoordinator>();
            var economy = services.Resolve<MobaSkillEconomyService>();
            var actorIds = registry.CopyActorIdsInOrder();
            if (actorIds.Count != 1 || !registry.TryGet(actorIds[0], out var actor))
                throw new InvalidOperationException("正式施法配方必须只有一名施法者。");
            var actorId = actorIds[0];
            var scope = new LocalStateSyncCastScope(recovery.LocalReplayGeneration, 1, stream, inputGeneration,
                1, actorId, registry.GetEntityVersion(actorId));
            var candidateId = 0;
            var session = recovery.AttachLocalBattleCast(in scope, delta,
                () => CreateBattleCastWorld($"scenario-cast-candidate-{++candidateId}", windup, movement, heroId, basicAttack, skillIds),
                observeCastProgress: observeCastProgress, movementEnabled: movement);
            var initial = Snapshot(0, 1, 0);
            var (initialEconomy, initialCoverage) = CaptureSettled(in initial);
            Commit(in initial, in initialEconomy, 0, in initialCoverage, CaptureMovement(in initial));
            var firstSequence = movement ? 2L : 1L;
            var pendingSequence = firstSequence + 1;
            var first = new SkillInputEvent(firstSlot, SkillInputPhase.Press,
                aimPos: new Vec3(0, 0, 5), aimDir: new Vec3(0, 0, 1), targetFrame: firstInput, inputSequence: firstSequence);
            var pending = new SkillInputEvent(secondSlot, SkillInputPhase.Press, targetActorId: actorId,
                aimPos: new Vec3(1, 0, 1), aimDir: new Vec3(0, 0, 1), targetFrame: pendingTick, inputSequence: 2);

            var checkpoints = new List<LocalStateSyncReplay.Checkpoint>(nextTick + 1);
            var reference = new LocalStateSyncBattleCastObservation[nextTick + 1];
            var anchor = default(GatewayStateSyncSnapshot);
            var anchorEconomy = default(LocalStateSyncCastEconomy);
            var anchorCoverage = default(LocalStateSyncBattleCastCoverage);
            LocalStateSyncMovementCut? anchorMovement = null;
            var steps = new List<LocalStateSyncReplay.SkillReplayStep>(deliveryTick - anchorTick);
            string oldRejection = null;
            long? replayed = null;
            for (var tick = 0; tick <= nextTick; tick++)
            {
                if (tick > 0)
                {
                    frame.StepTo(new FrameIndex(tick), delta);
                    if (movement && (tick == 2 || tick == pendingTick))
                    {
                        var intent = tick == 2 ? MobaActorIntent.MoveDirection(1f, 0f) : MobaActorIntent.MoveDirection(0f, 1f);
                        if (!MobaMoveInputCommandHandler.TryApplyDirection(actor, actorId, cast, in intent))
                            throw new InvalidOperationException($"正式权威参考拒绝 Tick {tick} 的移动。");
                    }
                    else if (windup && tick == authorityWindupInputTick)
                        CastReference(new SkillInputEvent(firstSlot, SkillInputPhase.Press,
                            aimPos: first.AimPos, aimDir: first.AimDir, targetFrame: tick, inputSequence: first.InputSequence));
                    else if (tick == pendingTick) CastReference(in pending);
                    else if (tick == firstInput && confirmed) CastReference(in first);
                    authority.Tick(delta);
                    var advanced = movement && tick == 1 ? session.AdvanceMovement(tick, 1, 1f, 0f) :
                        movement && tick == pendingTick ? session.AdvanceMovement(tick, pendingSequence, 0f, 1f) :
                        session.Advance(tick, tick == firstInput ? first : tick == pendingTick ? pending : (SkillInputEvent?)null);
                    if (!advanced || !session.HasPrediction || session.PublishedTick != tick)
                        throw new InvalidOperationException($"正式施法预测未推进到 Tick {tick}。", session.LastCandidateFailure);
                }
                reference[tick] = new LocalStateSyncBattleCastObservation(tick, ReadEconomy(), actor.transform.Value.Position,
                    observeCastProgress && cast.TryGetLatestRunning(actorId, out var active) ? active.ElapsedMs : (int?)null,
                    movement ? actor.transform.Value.Rotation : (Quat?)null);
                var beforeCommit = ToSkillState(session.Observation, movement);
                if (!windup && tick == pendingTick) oldRejection = session.LastInputRejection;
                if (tick == anchorTick)
                {
                    anchor = Snapshot(tick, 2, 1);
                    anchorMovement = CaptureMovement(in anchor);
                    if (windup)
                    {
                        var stamp = Stamp(in anchor);
                        var windupCut = LocalStateSyncWindupCut.Capture(authority, in stamp, firstSlot, secondSlot);
                        anchorEconomy = windupCut.Economy;
                        anchorCoverage = LocalStateSyncBattleCastCoverage.InFlight(windupCut);
                    }
                    else (anchorEconomy, anchorCoverage) = CaptureSettled(in anchor);
                }
                if (tick == deliveryTick)
                {
                    if (rejectBeforeRelease && (beforeCommit.CastElapsedMs is not int elapsed || elapsed >= releaseAtMs ||
                        beforeCommit.ManaRaw >= initialEconomy.ManaRaw || beforeCommit.PrimaryCooldownEndMs <= 0))
                        throw new InvalidOperationException("发射前拒绝配方需要仍在进行的正式施法、已扣法力和冷却。");
                    Commit(in anchor, in anchorEconomy, firstSequence, in anchorCoverage, anchorMovement);
                    if (session.PublishedTick != tick || session.PendingCount != (pendingTick == 0 ? 0 : 1) || candidateId != 2)
                        throw new InvalidOperationException("正式施法候选未在追平后接管，或错误裁剪了输入。");
                    foreach (var step in session.LastReplay)
                    {
                        if (!step.State.Equals(reference[step.Tick]) || step.Rejection != null ||
                            step.Input != (step.Tick == pendingTick ? pendingSequence : (long?)null))
                            throw new InvalidOperationException($"Tick {step.Tick} 的正式候选观测或输入与权威参考不符。");
                        if (step.Input.HasValue) replayed = step.Input;
                        steps.Add(new LocalStateSyncReplay.SkillReplayStep { Tick = step.Tick, State = ToSkillState(step.State, movement) });
                    }
                }
                if (readAppliedTick() != (tick < deliveryTick ? 0 : anchorTick) ||
                    (tick >= deliveryTick && !session.Observation.Equals(reference[tick])))
                    throw new InvalidOperationException("权威投影 Tick 不应随候选推进，发布结果须与权威参考一致。");

                checkpoints.Add(new LocalStateSyncReplay.Checkpoint
                {
                    Command = tick == deliveryTick ? "迟到施法截面" : tick == nextTick ? "接管后继续预测" : "正式战斗 Tick",
                    DeliveryTick = tick, BaselineTick = 0, Generation = scope.Generation, AppliedTick = readAppliedTick(),
                    AuthorityPose = movement ? readAuthorityProjection(actorId) : null,
                    Skill = new LocalStateSyncReplay.SkillTickObservation
                    {
                        Authority = ToSkillState(reference[tick], movement), OldPrediction = beforeCommit,
                        Published = ToSkillState(session.Observation, movement),
                    },
                });
            }

            var replay = new LocalStateSyncReplay
            {
                CaseId = "moba-local-formal-battle-cast",
                Variant = rejectBeforeRelease ? "lux-q-pre-release-rejected" : movement ? "battle-cast-move-windup" : windup ? "battle-cast-windup" :
                    confirmed ? "battle-cast-confirmed" : "battle-cast-rejected",
                Version = LocalStateSyncReplay.SkillTrackVersion, TickRate = BattleCastTickRate, Checkpoints = checkpoints.ToArray(),
                ActorModelId = movement ? heroId : (int?)null,
                SkillDelivery = new LocalStateSyncReplay.SkillDeliveryObservation
                {
                    PrimaryLabel = rejectBeforeRelease ? "Q 冷却结束（ms）" : windup ? "冷却结束（ms）" : "W（槽 2）",
                    SecondaryLabel = observeCastProgress ? "施法进度" : "E（槽 3）",
                    CastEconomy = true, FormalBattleCandidate = true, FormalWindup = windup, FormalMovement = movement,
                    FirstInputTick = firstInput, AnchorTick = anchorTick, InputTick = pendingTick, DeliveryTick = deliveryTick,
                    TerminalThroughSequence = session.TerminalThrough, PendingSequence = pendingTick == 0 ? 0 : pendingSequence,
                    ReplayedSequence = replayed, OldInputRejectCode = oldRejection, Steps = steps.ToArray(),
                },
            };
            replay.Validate();
            return replay;

            void CastReference(in SkillInputEvent input)
            {
                var result = cast.TryHandleInputResult(actorId, in input);
                if (!result.Success) throw new InvalidOperationException($"正式权威参考拒绝输入：{result.Code}。");
            }

            void Commit(in GatewayStateSyncSnapshot snapshot, in LocalStateSyncCastEconomy state, long through,
                in LocalStateSyncBattleCastCoverage coverage, LocalStateSyncMovementCut? movementCut)
            {
                var stamp = Stamp(in snapshot);
                var proof = new LocalStateSyncCastTerminalProof(in stamp, through);
                if (recovery.CommitLocalBattleCastSnapshot(session, in snapshot, in stamp, in state, proof, in coverage, movementCut) != LocalStateSyncCastResult.Published)
                    throw new InvalidOperationException("正式恢复入口未发布已追平的施法候选。", session.LastCandidateFailure);
            }

            LocalStateSyncCastStamp Stamp(in GatewayStateSyncSnapshot snapshot) =>
                new(in scope, snapshot.Frame, snapshot.SnapshotId);

            LocalStateSyncMovementCut? CaptureMovement(in GatewayStateSyncSnapshot snapshot)
            {
                if (!movement) return null;
                var stamp = Stamp(in snapshot);
                return LocalStateSyncMovementCut.Capture(authority, in stamp);
            }

            GatewayStateSyncSnapshot Snapshot(int tick, long revision, long baseline)
            {
                var position = actor.transform.Value.Position;
                var forward = actor.transform.Value.Forward;
                var yaw = movement ? (float)Math.Atan2(forward.X, forward.Z) : 0f;
                var hp = actor.resourceContainer.Value.Map[ResourceType.Hp];
                var sample = new GatewayStateSyncActorSnapshot(actorId, position.X, position.Y, position.Z, yaw, 0f, 0f,
                    hp.Current.ToSingle(), hp.LastMax.ToSingle(), (int)actor.team.Value, kind: (int)SpawnEntityKind.Character,
                    code: heroId, ownerNetId: 1);
                return new GatewayStateSyncSnapshot(1, tick, 0d, baseline == 0, new[] { sample }, ActorOnlySnapshotSchema,
                    eventEpoch: "scenario-epoch", usesFixedBaseline: true, stateStreamGeneration: stream, snapshotId: revision,
                    baseSnapshotId: baseline);
            }

            (LocalStateSyncCastEconomy Economy, LocalStateSyncBattleCastCoverage Coverage) CaptureSettled(in GatewayStateSyncSnapshot snapshot)
            {
                var tick = snapshot.Frame;
                if (frame.Frame.Value != tick || cast.TryGetLatestRunning(actorId, out _) || economy.PendingTransactionCount != 0 ||
                    economy.GetGlobalCooldownEndTimeMs(actorId) > tick * 1000L / BattleCastTickRate)
                    throw new InvalidOperationException($"Tick {tick} 仍有不可恢复的施法依赖。");
                var stamp = Stamp(in snapshot);
                return (ReadEconomy(), LocalStateSyncBattleCastCoverage.Settled(in stamp));
            }

            LocalStateSyncCastEconomy ReadEconomy() => new LocalStateSyncCastEconomy(
                actor.resourceContainer.Value.Map[ResourceType.Mana].Current.RawValue,
                ReadSlot(firstSlot), ReadSlot(secondSlot));

            LocalStateSyncCastSlot ReadSlot(int number)
            {
                var slot = actor.skillLoadout.ActiveSkills[number - 1];
                return new LocalStateSyncCastSlot(number, slot.SkillId, slot.Level, slot.CooldownEndTimeMs, slot.CooldownDurationMs);
            }
        }

        private static LocalStateSyncReplay.SkillState ToSkillState(LocalStateSyncBattleCastObservation observation, bool movement) =>
            new LocalStateSyncReplay.SkillState
            {
                ManaRaw = observation.Economy.ManaRaw,
                PrimaryCooldownEndMs = observation.Economy.First.EndMs,
                SecondaryCooldownEndMs = observation.Economy.Second.EndMs,
                CastElapsedMs = observation.CastElapsedMs,
                PositionX = movement ? observation.Position.X : (float?)null,
                PositionY = movement ? observation.Position.Y : (float?)null,
                PositionZ = movement ? observation.Position.Z : (float?)null,
                Pose = movement && observation.Rotation is { } rotation
                    ? ToPose(new Transform3(observation.Position, rotation, Vec3.One)) : null,
            };

        /// <summary>
        /// 量测现有双槽配方使用的世界工厂，不改变默认装配或记录格式。
        /// </summary>
        internal static WorldBuildTiming MeasureSettledBattleCastWorld()
        {
            var timing = new WorldBuildTiming();
            using var world = CreateBattleCastWorld("scenario-cast-build-measurement", false, false, BattleCastHero,
                30030101, new[] { 30030201, 30030301, 30030401 }, timing);
            return timing;
        }

        internal sealed class WorldBuildTiming
        {
            internal double ServiceSetupMs, WorldConstructionMs, InitializeMs, ConfigLoadMs;
        }

        private sealed class MeasuredConfigProfile : IMobaConfigLoadProfile
        {
            private readonly IMobaConfigLoadProfile _inner;
            private readonly WorldBuildTiming _timing;
            internal MeasuredConfigProfile(IMobaConfigLoadProfile inner, WorldBuildTiming timing)
            {
                _inner = inner;
                _timing = timing;
            }

            public string Name => _inner.Name;
            public void Load(MobaConfigDatabase database) => Measure(() => _inner.Load(database));
            public void Load(MobaConfigDatabase database, IMobaConfigLoadPipeline pipeline) =>
                Measure(() => _inner.Load(database, pipeline));

            private void Measure(Action load)
            {
                var timer = Stopwatch.StartNew();
                try { load(); }
                finally { _timing.ConfigLoadMs += timer.Elapsed.TotalMilliseconds; }
            }
        }

        private static EntitasWorld CreateBattleCastWorld(string id, bool windup, bool movement,
            int heroId, int basicAttack, int[] skillIds, WorldBuildTiming timing = null)
        {
            var timer = timing == null ? null : Stopwatch.StartNew();
            var launch = new MobaBattleLaunchSpec(id, id, id, MobaBattleWorldBlueprint.Type, id, Player,
                mapId: 1, gameplayId: 1, ruleSetId: 0, configVersion: 0, protocolVersion: 0, randomSeed: 123,
                tickRate: BattleCastTickRate, inputDelayFrames: 0, launchMode: MobaBattleLaunchMode.ViewFastEnter,
                syncMode: MobaBattleLaunchSyncMode.StateSync, authorityMode: MobaBattleLaunchAuthorityMode.LocalAuthority,
                players: new[] { new MobaPlayerLoadout(Player, 1, heroId, heroId, 1, basicAttack,
                    skillIds, 0, hasSpawnPosition: 1, enableBrainOnSpawn: false) });
            var builder = WorldServiceContainerFactory.CreateWithAttributes(WorldServiceProfile.All,
                new[] { typeof(WorldServiceContainerFactory).Assembly, typeof(MobaWorldBootstrapModule).Assembly },
                new[] { "AbilityKit" });
            var loader = new ResourcesTextAssetLoader();
            builder.RegisterInstance<ITextAssetLoader>(loader);
            builder.RegisterInstance<ITextAssetDirectoryLoader>(loader);
            if (timing != null)
                builder.TryRegister<IMobaConfigLoadProfile>(WorldLifetime.Singleton, _ =>
                    new MeasuredConfigProfile(new LubanGroupsMobaConfigLoadProfile(loader), timing));
            builder.AddModule(new MobaConfigWorldModule());
            builder.RegisterInstance(launch.ToWorldInitData(MobaWorldBootstrapModule.InitOpCode));
            builder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new FrameTime());
            var options = new WorldCreateOptions(new WorldId(id), MobaBattleWorldBlueprint.Type) { ServiceBuilder = builder };
            new MobaBattleWorldBlueprint().Configure(options);
            if (timing != null)
            {
                timing.ServiceSetupMs = timer.Elapsed.TotalMilliseconds;
                timer.Restart();
            }
            var world = new EntitasWorld(options);
            try
            {
                if (timing != null)
                {
                    timing.WorldConstructionMs = timer.Elapsed.TotalMilliseconds;
                    timer.Restart();
                }
                world.Initialize();
                if (timing != null) timing.InitializeMs = timer.Elapsed.TotalMilliseconds;
                if (windup) InstallWindupRecipe(world);
                if (movement)
                {
                    var actors = world.Services.Resolve<MobaActorRegistry>();
                    var ids = actors.CopyActorIdsInOrder();
                    if (ids.Count != 1 || !actors.TryGet(ids[0], out var actor))
                        throw new InvalidOperationException("普通移动配方缺少角色。");
                    LocalStateSyncBattleCastWorld.ConfigureIsolatedLocomotion(actor);
                }
                return world;
            }
            catch
            {
                world.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 只配置当前配方世界的无目标前摇：正式经济阶段、提交点和等待，不读取或写回生产资源。
        /// </summary>
        private static void InstallWindupRecipe(EntitasWorld world)
        {
            const int windupFlow = 982001, settleFlow = 982002;
            var registry = world.Services.Resolve<MobaActorRegistry>();
            var ids = registry.CopyActorIdsInOrder();
            if (ids.Count != 1 || !registry.TryGet(ids[0], out var actor))
                throw new InvalidOperationException("前摇配方必须只有一名角色。");
            var skillId = actor.skillLoadout.ActiveSkills[BattleCastFirstSlot - 1].SkillId;
            var skill = new SkillDTO { Id = skillId, Name = "离线前摇", PreCastFlowId = windupFlow, CastFlowId = settleFlow,
                AllowCastWithoutTarget = true };
            var reload = world.Services.Resolve<MobaConfigDatabase>().ReloadFromDtoArrays(new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[] { skill },
                [typeof(SkillFlowDTO)] = new[]
                {
                    new SkillFlowDTO { Id = windupFlow, Phases = new[]
                    {
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Economy, PhaseId = "reserve",
                            Economy = new SkillEconomyPhaseDTO { Operation = (int)SkillEconomyOperation.ReserveCast,
                                ResourceType = (int)ResourceType.Mana, ResourceAmount = 10, UseResolvedResourceCost = false,
                                ChargeCost = 0, MaxCharges = 0, RequireExplicitCommit = true,
                                StartSkillCooldown = true, SkillCooldownMs = 500, UseResolvedSkillCooldown = false } },
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.CommitPoint, PhaseId = "commit",
                            CommitPoint = new SkillCommitPointPhaseDTO { RequireEconomyReservation = true } },
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Delay, PhaseId = "windup",
                            Delay = new SkillDelayPhaseDTO { DelayMs = 250 } },
                    } },
                    new SkillFlowDTO { Id = settleFlow, Phases = new[]
                    {
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Delay, PhaseId = "finish",
                            Delay = new SkillDelayPhaseDTO { DelayMs = 1 } },
                    } },
                },
            }, strict: false);
            if (!reload.Succeeded) throw new InvalidOperationException("无法配置离线前摇配方。");
        }
    }
}
