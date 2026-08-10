using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;

namespace AbilityKit.Demo.Moba.Services
{
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
    }

    public readonly struct MobaInputCommandResult
    {
        public readonly bool Succeeded;
        public readonly MobaInputCommandFailureCode FailureCode;
        public readonly string Message;
        public readonly string PlayerId;
        public readonly int ActorId;
        public readonly int OpCode;

        public MobaInputCommandResult(
            bool succeeded,
            MobaInputCommandFailureCode failureCode,
            string message,
            string playerId,
            int actorId,
            int opCode)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            Message = message;
            PlayerId = playerId;
            ActorId = actorId;
            OpCode = opCode;
        }

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
