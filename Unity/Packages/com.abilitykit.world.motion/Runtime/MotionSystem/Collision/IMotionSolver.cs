using AbilityKit.Ability.Share.Common.MotionSystem.Core;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Collision
{
    public interface IMotionSolver
    {
        MotionSolveResult Solve(int id, in MotionState state, in MotionOutput input, float dt);
    }

    public sealed class NoMotionSolver : IMotionSolver
    {
        public static readonly NoMotionSolver Instance = new NoMotionSolver();

        private NoMotionSolver() { }

        public MotionSolveResult Solve(int id, in MotionState state, in MotionOutput input, float dt)
        {
            return MotionSolveResult.NoHit(input.DesiredDelta);
        }
    }
}
