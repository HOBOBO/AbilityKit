using AbilityKit.Demo.Moba.Services.Buffs.Core;

namespace AbilityKit.Demo.Moba.Services.Buffs
{
    /// <summary>A read-only capability for exactly one target/source Buff revision.</summary>
    public readonly struct MobaSourceBuffToken
    {
        public readonly int TargetActorId, BuffId, SourceActorId;
        public readonly long SourceContextId, RuntimeContextId, RuntimeContextVersion;
        internal MobaSourceBuffToken(int target, int buff, int source, long context, long runtime, long version)
        { TargetActorId = target; BuffId = buff; SourceActorId = source; SourceContextId = context; RuntimeContextId = runtime; RuntimeContextVersion = version; }
    }

    public sealed partial class MobaBuffService
    {
        public bool TryReadSourceBuff(int targetActorId, int buffId, int sourceActorId, out MobaSourceBuffToken token)
        {
            token = default;
            var target = TryGetActorEntity(targetActorId);
            if (sourceActorId <= 0 || target == null || !target.hasBuffs || target.buffs.Active == null) return false;
            var found = false;
            foreach (var buff in target.buffs.Active)
            {
                if (buff == null || buff.BuffId != buffId || buff.SourceId != sourceActorId) continue;
                // Ambiguous content is rejected rather than consuming arbitrary instances.
                if (found) { token = default; return false; }
                found = true;
                token = new MobaSourceBuffToken(targetActorId, buffId, sourceActorId, buff.SourceContextId,
                    buff.RuntimeContextId, buff.RuntimeContextVersion);
            }
            return found;
        }

        public bool TryClaimSourceBuff(in MobaSourceBuffToken token)
        {
            if (_draining > 0 || token.SourceContextId == 0 || token.RuntimeContextId == 0) return false;
            _pending.Add(new BuffCommand
            {
                Seq = ++_nextCommandSeq, Kind = BuffCommandKind.Remove,
                RemoveRequest = new BuffRemoveRequest
                {
                    TargetActorId = token.TargetActorId, BuffId = token.BuffId, SourceActorId = token.SourceActorId,
                    SourceContextId = token.SourceContextId, ExpectedRuntimeContextId = token.RuntimeContextId,
                    ExpectedRuntimeContextVersion = token.RuntimeContextVersion, Reason = MobaExecutionEndReason.Completed,
                },
            });
            // The lifecycle removes the old instance before invoking any removal callbacks.
            return DrainImmediateCommand(_nextCommandSeq, 256);
        }
    }
}
