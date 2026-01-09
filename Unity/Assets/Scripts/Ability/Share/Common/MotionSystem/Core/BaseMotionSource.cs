using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Core
{
    public abstract class BaseMotionSource : IMotionSource
    {
        protected BaseMotionSource(int groupId, MotionStacking stacking, int priority)
        {
            GroupId = groupId;
            Stacking = stacking;
            Priority = priority;
        }

        public int GroupId { get; }

        public MotionStacking Stacking { get; }

        public int Priority { get; }

        public abstract bool IsActive { get; }

        public abstract void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta);

        public abstract void Cancel();
    }
}
