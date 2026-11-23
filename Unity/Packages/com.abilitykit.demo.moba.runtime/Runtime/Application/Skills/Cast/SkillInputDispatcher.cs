using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SkillInputDispatcher
    {
        private readonly SkillCastCoordinator _executor;

        public SkillInputDispatcher(SkillCastCoordinator executor)
        {
            _executor = executor;
        }

        public MobaSkillInputHandleResult Dispatch(int actorId, in SkillInputEvent evt)
        {
            return _executor.TryHandleInputResult(actorId, in evt);
        }
    }
}

