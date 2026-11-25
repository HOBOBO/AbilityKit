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
    public sealed class MobaInputCoordinator : LogicWorldInputCoordinatorBase<MobaInputCommandContext>, IMobaInputCoordinator,
        IWorldInputReplaySink, IWorldSettledTimelineSink
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
        private bool _clientBound;

        /// <summary>
        /// 开始捕获权威输入。调用方须在世界通过帧完成检查后提交捕获。
        /// </summary>
        public MobaAuthorityFrameCapture BeginAuthorityFrameCapture(FrameIndex frame)
        {
            if (_clientBound || _replayActive || _actionAcks == null)
                throw new InvalidOperationException("Authority frame capture requires a ready authority coordinator.");
            if (frame.Value < 0) throw new ArgumentOutOfRangeException(nameof(frame));

            return new MobaAuthorityFrameCapture(frame, _actionAcks,
                (inputs, before, after) => TrySubmitWithContext(frame, inputs,
                    CreateContext(MobaInputExecutionKind.Authority), before, after));
        }

        /// <summary>
        /// 观察正常权威派发的逐命令结果，不消费动作回执或改变原提交结果。
        /// 调用方只能在同一世界 Tick 完成后使用这些观察值。
        /// </summary>
        internal LogicWorldInputSubmitResult TrySubmitWithAuthorityObservation(FrameIndex frame,
            IReadOnlyList<PlayerInputCommand> inputs, Action<int, MobaFrameInputDisposition> observe)
        {
            if (_clientBound || _replayActive || observe == null) return TrySubmit(frame, inputs);
            return TrySubmitWithContext(frame, inputs, null, afterCommand: (ordinal, result) =>
                observe(ordinal, result.ClassifyAuthorityDisposition()));
        }

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
            if (_clientBound) _actionAcks?.BindClientInput();
            services.TryResolve(out _inputEventSink);
            services.TryResolve(out _exceptionPolicy);
            if (_skills != null) return;

            ResolveSkillExecutor(services);
        }

        protected override MobaInputCommandContext CreateContext(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            return CreateContext(_replayActive ? MobaInputExecutionKind.Replay :
                _clientBound ? MobaInputExecutionKind.Received : MobaInputExecutionKind.Authority);
        }

        private MobaInputCommandContext CreateContext(MobaInputExecutionKind executionKind) =>
            new MobaInputCommandContext(_phase, _playerActorMap, _entities, _skills, Services, executionKind);

        void IWorldClientInputSink.BindClientInput()
        {
            _clientBound = true;
            _actionAcks?.BindClientInput();
        }

        void IWorldSettledTimelineSink.BindSettledTimeline()
        {
            if (!_clientBound || _actionAcks == null)
                throw new InvalidOperationException("Settled timeline requires a ready client coordinator.");
            _actionAcks.BindSettledTimeline();
        }

        IWorldSettledFrameUpdate IWorldSettledTimelineSink.PrepareSettledFrame(WorldSettledInputFrame frame)
        {
            if (!(frame.Metadata is MobaSettledFrameMetadata metadata) || frame.ExecutedInputs.Count > metadata.CommandCount)
                throw new InvalidOperationException("Invalid MOBA settled frame metadata.");
            return _actionAcks.PrepareSettledFrame(frame.Frame, metadata);
        }

        void IWorldClientInputSink.SubmitPredicted(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) =>
            SubmitClientInput(frame, inputs, MobaInputExecutionKind.Predicted);

        void IWorldClientInputSink.SubmitReceived(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) =>
            SubmitClientInput(frame, inputs, MobaInputExecutionKind.Received);

        /// <summary>
        /// 提交本地预测，返回实际执行项；成功的幂等重复不属于执行项。
        /// </summary>
        public PlayerInputCommand[] ExecutePredictedInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (!_clientBound || _replayActive)
                throw new InvalidOperationException("Observed prediction requires a client coordinator outside legacy input replay.");
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Count == 0) return Array.Empty<PlayerInputCommand>();

            var executed = new List<PlayerInputCommand>();
            var submit = TrySubmitWithContext(frame, inputs, CreateContext(MobaInputExecutionKind.Predicted),
                afterCommand: (ordinal, result) =>
                {
                    if (result.Succeeded && !result.IsDuplicate) executed.Add(inputs[ordinal]);
                });
            if (!submit.Succeeded)
                throw new InvalidOperationException($"Prediction input dispatch failed. frame={frame.Value}: {submit}");
            return executed.ToArray();
        }

        /// <summary>
        /// 执行已验证结算记录中的实际执行项，不以本地 Ack 重新受理。失败时调用方不得提交该帧。
        /// </summary>
        public void ExecuteSettledInputs(FrameIndex frame, IReadOnlyList<PlayerInputCommand> executedInputs)
        {
            if (!_clientBound || _replayActive)
                throw new InvalidOperationException("Settled input requires a client coordinator outside legacy input replay.");
            if (executedInputs == null) throw new ArgumentNullException(nameof(executedInputs));
            if (executedInputs.Count == 0) return;

            var submit = TrySubmitWithContext(frame, executedInputs, CreateContext(MobaInputExecutionKind.Settled),
                afterCommand: (ordinal, result) =>
                {
                    if (!result.Succeeded || result.IsDuplicate)
                        throw new InvalidOperationException($"Settled command did not execute. frame={frame.Value}, ordinal={ordinal}: {result}");
                });
            if (!submit.Succeeded || submit.HandledCount != executedInputs.Count)
                throw new InvalidOperationException($"Settled inputs did not execute. frame={frame.Value}: {submit}");
        }

        private void SubmitClientInput(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs, MobaInputExecutionKind executionKind)
        {
            if (!_clientBound || _replayActive)
                throw new InvalidOperationException("Client input requires a bound coordinator outside active replay.");
            if (inputs == null || inputs.Count == 0) return;
            TrySubmitWithContext(frame, inputs, CreateContext(executionKind));
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

