using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Common.MotionSystem.Collision;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Events
{
    public interface IMotionEventSink
    {
        void OnHit(int id, in MotionState state, in MotionHit hit);
        void OnArrive(int id, in MotionState state);
        void OnExpired(int id, in MotionState state);
    }
}
