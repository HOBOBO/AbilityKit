using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed partial class MobaSkillCastRuntimeService
    {
        private static readonly MobaSkillRuntimeBlackboardKey PresentationOrigin =
            new MobaSkillRuntimeBlackboardKey(140, "skill.presentation.origin", MobaSkillRuntimeValueKind.Vec3, ownerModuleId: 1002);
        private static readonly MobaSkillRuntimeBlackboardKey PresentationForward =
            new MobaSkillRuntimeBlackboardKey(141, "skill.presentation.forward", MobaSkillRuntimeValueKind.Vec3, ownerModuleId: 1002);

        public bool TryCapturePresentationFrame(in MobaSkillCastRuntimeHandle handle, int casterActorId,
            in Vec3 origin, in Vec3 forward)
        {
            var value = new CastPresentationFrame(origin.X, origin.Y, origin.Z, forward.X, forward.Y, forward.Z);
            if (!value.IsValid || !TryGet(in handle, out var cast) || cast.IsEnding ||
                cast.RootContextId != handle.RootContextId || cast.CasterActorId != casterActorId) return false;
            // First capture wins, including nested callbacks and auxiliary casts.
            if (cast.Blackboard.TryGetVec3(in PresentationOrigin, out _)) return true;
            return cast.Blackboard.SetVec3(in PresentationOrigin, in origin) &&
                cast.Blackboard.SetVec3(in PresentationForward, in forward);
        }

        public bool TryGetPresentationFrame(long castContextId, int casterActorId, out CastPresentationFrame frame)
        {
            frame = default;
            if (!TryGetByContext(castContextId, out var cast) || cast.IsEnding ||
                cast.CasterActorId != casterActorId ||
                !cast.Blackboard.TryGetVec3(in PresentationOrigin, out var origin) ||
                !cast.Blackboard.TryGetVec3(in PresentationForward, out var forward)) return false;
            frame = new CastPresentationFrame(origin.X, origin.Y, origin.Z, forward.X, forward.Y, forward.Z);
            return frame.IsValid;
        }
    }
}
