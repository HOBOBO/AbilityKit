using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 正常权威派发的结果类别，既不是输入入队回执，也不证明世界已提交。
    /// </summary>
    public enum MobaFrameInputDisposition : byte
    {
        Unknown,
        Executed,
        Rejected,
        Duplicate
    }

    public enum MobaInputCommandFailureCode
    {
        None = 0,
        ContextMissing = 1,
        NotInGame = 2,
        ActorMapMissing = 3,
        ActorEntityMissing = 4,
        TransformMissing = 5,
        PayloadMissing = 6,
        SkillExecutorMissing = 7,
        SkillRejected = 8,
        MissingHandler = 9,
        HandlerRejected = 10,
        PayloadInvalid = 11,
        ContractMissing = 12,
        AuthorityRejected = 13,
        FramePolicyRejected = 14,
        PredictionIdentityRejected = 15,
        InputSequenceRejected = 16,
        InterruptEpochRejected = 17,
    }

    public readonly struct MobaInputCommandResult
    {
        public readonly bool Succeeded;
        public readonly MobaInputCommandFailureCode FailureCode;
        public readonly string Message;
        public readonly string PlayerId;
        public readonly int ActorId;
        public readonly int OpCode;
        public readonly long DiagnosticCommandId;
        public readonly int SkillSlot;
        public readonly int SkillPhase;
        public readonly int TargetActorId;
        public readonly MobaSkillCastRuntimeHandle SkillRuntimeHandle;
        internal readonly bool IsDuplicate;
        private readonly bool _isDefinitiveSkillInputRejection;

        internal MobaFrameInputDisposition ClassifyStateSyncDisposition()
        {
            var disposition = ClassifyAuthorityDisposition();
            return disposition == MobaFrameInputDisposition.Unknown && _isDefinitiveSkillInputRejection
                ? MobaFrameInputDisposition.Rejected
                : disposition;
        }

        internal MobaFrameInputDisposition ClassifyAuthorityDisposition()
        {
            if (IsDuplicate) return MobaFrameInputDisposition.Duplicate;
            if (Succeeded) return MobaFrameInputDisposition.Executed;
            switch (FailureCode)
            {
                case MobaInputCommandFailureCode.AuthorityRejected:
                case MobaInputCommandFailureCode.FramePolicyRejected:
                case MobaInputCommandFailureCode.PredictionIdentityRejected:
                case MobaInputCommandFailureCode.InputSequenceRejected:
                case MobaInputCommandFailureCode.InterruptEpochRejected:
                case MobaInputCommandFailureCode.PayloadInvalid:
                case MobaInputCommandFailureCode.PayloadMissing:
                    return MobaFrameInputDisposition.Rejected;
                default:
                    return MobaFrameInputDisposition.Unknown;
            }
        }

        public MobaInputCommandResult(
            bool succeeded,
            MobaInputCommandFailureCode failureCode,
            string message,
            string playerId,
            int actorId,
            int opCode,
            long diagnosticCommandId = 0L,
            int skillSlot = 0,
            int skillPhase = 0,
            int targetActorId = 0,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
            : this(succeeded, failureCode, message, playerId, actorId, opCode,
                diagnosticCommandId, skillSlot, skillPhase, targetActorId, skillRuntimeHandle, false, false)
        {
        }

        private MobaInputCommandResult(bool succeeded, MobaInputCommandFailureCode failureCode, string message,
            string playerId, int actorId, int opCode, long diagnosticCommandId, int skillSlot, int skillPhase,
            int targetActorId, MobaSkillCastRuntimeHandle skillRuntimeHandle, bool isDuplicate,
            bool isDefinitiveSkillInputRejection)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            Message = message;
            PlayerId = playerId;
            ActorId = actorId;
            OpCode = opCode;
            DiagnosticCommandId = diagnosticCommandId;
            SkillSlot = skillSlot;
            SkillPhase = skillPhase;
            TargetActorId = targetActorId;
            SkillRuntimeHandle = skillRuntimeHandle;
            IsDuplicate = isDuplicate;
            _isDefinitiveSkillInputRejection = isDefinitiveSkillInputRejection;
        }

        public MobaInputCommandResult WithDiagnosticCommandId(long commandId)
        {
            return new MobaInputCommandResult(Succeeded, FailureCode, Message, PlayerId, ActorId,
                OpCode, commandId, SkillSlot, SkillPhase, TargetActorId, SkillRuntimeHandle, IsDuplicate,
                _isDefinitiveSkillInputRejection);
        }

        public MobaInputCommandResult WithSkillDiagnostic(int slot, int phase, int targetActorId,
            in MobaSkillCastRuntimeHandle runtimeHandle)
        {
            return new MobaInputCommandResult(Succeeded, FailureCode, Message, PlayerId, ActorId,
                OpCode, DiagnosticCommandId, slot, phase, targetActorId, runtimeHandle, IsDuplicate,
                _isDefinitiveSkillInputRejection);
        }

        internal MobaInputCommandResult WithSkillInputRejection(in MobaSkillInputHandleResult result) =>
            new MobaInputCommandResult(Succeeded, FailureCode, Message, PlayerId, ActorId, OpCode,
                DiagnosticCommandId, SkillSlot, SkillPhase, TargetActorId, SkillRuntimeHandle, IsDuplicate,
                !Succeeded && FailureCode == MobaInputCommandFailureCode.SkillRejected && result.IsDefinitiveInputRejection);

        internal MobaInputCommandResult AsDuplicate() =>
            new MobaInputCommandResult(Succeeded, FailureCode, Message, PlayerId, ActorId,
                OpCode, DiagnosticCommandId, SkillSlot, SkillPhase, TargetActorId, SkillRuntimeHandle, true,
                _isDefinitiveSkillInputRejection);

        public static MobaInputCommandResult Accepted(PlayerInputCommand command, int actorId = 0)
        {
            return new MobaInputCommandResult(true, MobaInputCommandFailureCode.None, null, command.Player.Value, actorId, command.OpCode);
        }

        public static MobaInputCommandResult Accepted(PlayerInputCommand command, string message, int actorId = 0)
        {
            return new MobaInputCommandResult(true, MobaInputCommandFailureCode.None, message, command.Player.Value, actorId, command.OpCode);
        }

        public static MobaInputCommandResult Rejected(
            PlayerInputCommand command,
            MobaInputCommandFailureCode failureCode,
            int actorId = 0)
        {
            return new MobaInputCommandResult(false, failureCode, null, command.Player.Value, actorId, command.OpCode);
        }

        public static MobaInputCommandResult Rejected(
            PlayerInputCommand command,
            MobaInputCommandFailureCode failureCode,
            string message,
            int actorId = 0)
        {
            return new MobaInputCommandResult(false, failureCode, message, command.Player.Value, actorId, command.OpCode);
        }

        public override string ToString()
        {
            return Succeeded
                ? $"Accepted(Player={PlayerId},Actor={ActorId},Op={OpCode},Message={Message})"
                : $"Rejected(Code={FailureCode},Player={PlayerId},Actor={ActorId},Op={OpCode},Message={Message})";
        }
    }

    public static class MobaInputDiagnostics
    {
        public static void RecordCommandException(
            IMobaBattleDiagnosticsService diagnostics,
            FrameIndex frame,
            PlayerInputCommand command,
            Exception exception,
            string owner)
        {
            var key = "input.command.exception";
            var message = $"Input command handler threw. frame={frame.Value} player={command.Player.Value} op={command.OpCode} exception={exception.GetType().Name}: {exception.Message}";
            if (diagnostics != null)
            {
                diagnostics.Exception(key, exception, message);
                diagnostics.Counter(MobaBattleDiagnosticMetric.InputCommandException);
                diagnostics.Counter(key);
                diagnostics.RecordInputCommandException();
                return;
            }

            MobaRuntimeLog.Exception(exception, MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Exception, owner, message);
        }

        public static void RecordCommandRejected(
            IMobaBattleDiagnosticsService diagnostics,
            FrameIndex frame,
            in MobaInputCommandResult result,
            string owner)
        {
            if (result.Succeeded) return;

            var key = "input.command.rejected." + result.FailureCode;
            var capturedResult = result;
            if (diagnostics != null)
            {
                diagnostics.Warning(key, () => FormatCommandRejectedMessage(frame, in capturedResult));
                diagnostics.Counter(MobaBattleDiagnosticMetric.InputCommandRejected);
                diagnostics.Counter(key);
                diagnostics.RecordInputCommandRejected(result.FailureCode);
                return;
            }

            MobaRuntimeLog.Warning(
                MobaRuntimeLogModule.Input,
                MobaRuntimeLogPurpose.Rejection,
                owner,
                () => FormatCommandRejectedMessage(frame, in capturedResult));
        }

        public static void RecordBatchWarning(
            IMobaBattleDiagnosticsService diagnostics,
            string key,
            string message,
            string owner)
        {
            if (diagnostics != null)
            {
                diagnostics.Warning(key, message);
                diagnostics.Counter(key);
                return;
            }

            MobaRuntimeLog.Warning(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Rejection, owner, message);
        }

        public static void RecordBatchWarning(
            IMobaBattleDiagnosticsService diagnostics,
            string key,
            Func<string> messageFactory,
            string owner)
        {
            if (messageFactory == null) return;
            if (diagnostics != null)
            {
                diagnostics.Warning(key, messageFactory);
                diagnostics.Counter(key);
                return;
            }

            MobaRuntimeLog.Warning(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Rejection, owner, messageFactory);
        }

        public static void RecordBatchAccepted(IMobaBattleDiagnosticsService diagnostics, int acceptedCount, int handledCount)
        {
            diagnostics?.Counter(MobaBattleDiagnosticMetric.InputBatchAccepted);
            diagnostics?.Sample(MobaBattleDiagnosticMetric.InputBatchAcceptedCount, acceptedCount);
            diagnostics?.Sample(MobaBattleDiagnosticMetric.InputBatchHandledCount, handledCount);
            diagnostics?.RecordInputBatchAccepted(acceptedCount, handledCount);
        }

        private static string FormatCommandRejectedMessage(FrameIndex frame, in MobaInputCommandResult result)
        {
            var reason = string.IsNullOrEmpty(result.Message) ? result.FailureCode.ToString() : result.Message;
            return $"Input command rejected. frame={frame.Value} code={result.FailureCode} player={result.PlayerId} actor={result.ActorId} op={result.OpCode} reason={reason}";
        }
    }
}
