using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 逻辑世界输入协调器：负责玩法侧上下文构建和命令处理器分发。
    /// </summary>
    [WorldService(typeof(IWorldInputSink))]
    [WorldService(typeof(IWorldInputReplaySink))]
    [WorldService(typeof(IMobaInputCoordinator))]
    [WorldService(typeof(MobaInputCoordinator))]
    public sealed class MobaInputCoordinator : LogicWorldInputCoordinatorBase<MobaInputCommandContext>, IMobaInputCoordinator, IWorldInputReplaySink
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaEntityManager _entities;
        private readonly MobaInputCommandContractRegistry _contracts;
        private readonly MobaInputCommandHandlerRegistry _handlers;

        private SkillCastCoordinator _skills;
        private MobaActionAckSnapshotService _actionAcks;
        private IMobaBattleDiagnosticEventSink _inputEventSink;
        private IMobaBattleExceptionPolicy _exceptionPolicy;
        private long _nextDiagnosticCommandId;
        private bool _replayActive;

        public MobaInputCoordinator(MobaLogicWorldRunGateService phase, MobaPlayerActorMapService playerActorMap, MobaEntityManager entities, MobaInputCommandContractRegistry contracts)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
            _handlers = _contracts.HandlerRegistry;
        }

        protected override void OnServicesReady(IWorldResolver services)
        {
            if (services == null) return;

            _handlers.BindHandlers(services);
            services.TryResolve(out _actionAcks);
            services.TryResolve(out _inputEventSink);
            services.TryResolve(out _exceptionPolicy);
            if (_skills != null) return;

            ResolveSkillExecutor(services);
        }

        protected override MobaInputCommandContext CreateContext(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            return new MobaInputCommandContext(_phase, _playerActorMap, _entities, _skills, Services, _replayActive);
        }

        public void BeginReplay(FrameIndex restoredFrame, FrameIndex replayToFrame)
        {
            if (_replayActive)
                throw new InvalidOperationException("An input replay is already active.");
            if (replayToFrame.Value < restoredFrame.Value)
                throw new ArgumentOutOfRangeException(nameof(replayToFrame));

            _actionAcks?.BeginReplay(restoredFrame, replayToFrame);
            _replayActive = true;
        }

        public void Replay(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (!_replayActive)
                throw new InvalidOperationException("BeginReplay must be called before replaying input.");
            if (inputs == null || inputs.Count == 0) return;

            var submit = TrySubmit(frame, inputs);
            if (!submit.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Input replay failed at frame {frame.Value}: {submit}");
            }
        }

        public void EndReplay()
        {
            if (!_replayActive) return;
            _replayActive = false;
            _actionAcks?.EndReplay();
        }

        protected override bool Dispatch(MobaInputCommandContext context, FrameIndex frame, PlayerInputCommand command, out MobaInputCommandResult result)
        {
            context.DiagnosticCommandId = NextDiagnosticCommandId();
            if (!_contracts.TryValidateCommand(context, frame, command, out result))
            {
                result = result.WithDiagnosticCommandId(context.DiagnosticCommandId);
                CollectInputCommand(frame, in result);
                return false;
            }

            var handled = _handlers.TryHandle(context, frame, command, out result);
            result = result.WithDiagnosticCommandId(context.DiagnosticCommandId);
            CollectInputCommand(frame, in result);
            return handled;
        }

        private long NextDiagnosticCommandId()
        {
            _nextDiagnosticCommandId++;
            if (_nextDiagnosticCommandId <= 0L) _nextDiagnosticCommandId = 1L;
            return _nextDiagnosticCommandId;
        }

        private void CollectInputCommand(FrameIndex frame, in MobaInputCommandResult result)
        {
            try
            {
                var sink = _inputEventSink;
                if (sink == null || !sink.IsEnabled(BattleDiagnosticEventChannel.Input)) return;
                var payloadData = new BattleDiagnosticInputCommandPayload(
                    result.DiagnosticCommandId, frame.Value, result.PlayerId, result.OpCode,
                    result.Succeeded, (int)result.FailureCode, result.Message,
                    result.SkillSlot, result.SkillPhase, result.TargetActorId);
                var payload = BattleDiagnosticEventPayload.FromInputCommand(in payloadData);
                var runtimeHandle = result.SkillRuntimeHandle;
                var runtime = runtimeHandle.IsValid
                    ? new BattleDiagnosticRuntimeHandle(runtimeHandle.RuntimeId, runtimeHandle.Generation)
                    : default;
                var rootContextId = runtimeHandle.IsValid ? runtimeHandle.RootContextId : 0L;
                var draft = new MobaBattleDiagnosticEventDraft(
                    BattleDiagnosticEventKind.InputCommand, BattleDiagnosticEventChannel.Input,
                    result.Succeeded ? BattleDiagnosticEventOutcome.Succeeded : BattleDiagnosticEventOutcome.Failed,
                    sourceActorId: result.ActorId,
                    targetActorId: result.TargetActorId,
                    rootContextId: rootContextId,
                    contextId: rootContextId,
                    skillRuntime: runtime,
                    payloadVersion: BattleDiagnosticInputCommandPayload.CurrentSchemaVersion,
                    summary: $"command={result.DiagnosticCommandId} op={result.OpCode} " +
                             $"result={(result.Succeeded ? "accepted" : result.FailureCode.ToString())}",
                    payload: payload);
                sink.TryCollect(in draft);
            }
            catch (Exception ex)
            {
                ReportDiagnosticCollectionException(ex, frame, in result);
            }
        }

        private void ReportDiagnosticCollectionException(
            Exception exception,
            FrameIndex frame,
            in MobaInputCommandResult result)
        {
            var context = new MobaBattleExceptionContext(
                MobaBattleExceptionDomain.Input,
                "input.diagnostics.collect",
                actorId: result.ActorId,
                detail: $"frame={frame.Value} command={result.DiagnosticCommandId} op={result.OpCode}");

            try
            {
                if (_exceptionPolicy != null &&
                    _exceptionPolicy.TryHandle(exception, in context, MobaBattleExceptionSeverity.Recoverable))
                {
                    return;
                }
            }
            catch (Exception policyException)
            {
                MobaRuntimeLog.Exception(
                    policyException,
                    MobaRuntimeLogModule.Input,
                    MobaRuntimeLogPurpose.Exception,
                    nameof(MobaInputCoordinator),
                    "Input diagnostic exception policy failed.");
            }

            MobaRuntimeLog.Exception(
                exception,
                MobaRuntimeLogModule.Input,
                MobaRuntimeLogPurpose.Exception,
                nameof(MobaInputCoordinator),
                context.BuildMessage(MobaBattleExceptionSeverity.Recoverable));
        }

        private void ResolveSkillExecutor(IWorldResolver services)
        {
            try
            {
                _skills = services.Resolve<SkillCastCoordinator>();
                if (_skills == null)
                {
                    MobaRuntimeLog.Error(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Validation, nameof(MobaInputCoordinator), "SkillCastCoordinator resolved as null.");
                }
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(ex, MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Exception, nameof(MobaInputCoordinator), "Failed to resolve SkillCastCoordinator.");
                MobaDependencyResolveDiagnostics.LogSkillExecutionDependencies(services, nameof(MobaInputCoordinator));
            }
        }

        public override void Dispose()
        {
            try
            {
                EndReplay();
            }
            finally
            {
                _actionAcks = null;
                _inputEventSink = null;
                _exceptionPolicy = null;
                _skills = null;
                _nextDiagnosticCommandId = 0L;
                base.Dispose();
            }
        }

    }
}

