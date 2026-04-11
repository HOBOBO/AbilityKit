using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Trajectory
{
    public interface ITrajectory3D
    {
        float Duration { get; }

        Vec3 SamplePosition(float time);

        bool TrySampleForward(float time, out Vec3 forward);
    }
}
