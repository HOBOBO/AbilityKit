using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Generic
{
    /// <summary>
    /// 只通过显式状态推进路径，不持有跨帧字段或运行时依赖。
    /// </summary>
    internal static class PathFollowerStepper
    {
        /*
         * 保留原来源的分支及浮点运算顺序：每次最多向一个未到达的目标点贡献位移。
         * 来源不提交位置，也不因微小距离分支已越过末点而提前发出结束通知。
         */
        internal static void Tick(ref PathFollowerState path, ref MotionState state, float dt, float speed, ref Vec3 outDesiredDelta)
        {
            if (!path.IsActive) return;
            if (dt <= 0f) return;

            if (path.Points.Length == 0)
            {
                path.IsActive = false;
                return;
            }

            while (path.Index < path.Points.Length)
            {
                var to = path.Points[path.Index] - state.Position;
                if (to.SqrMagnitude > path.ArriveEpsilon * path.ArriveEpsilon) break;
                path.Index++;
            }

            if (path.Index >= path.Points.Length)
            {
                path.IsActive = false;
                return;
            }

            var target = path.Points[path.Index];
            var deltaTo = target - state.Position;
            var dist = DeterministicMathBridge.Magnitude(in deltaTo);
            if (dist <= 0.00001f)
            {
                path.Index++;
                return;
            }

            var maxStep = speed * dt;
            if (maxStep <= 0f) return;

            var step = maxStep;
            if (step > dist) step = dist;

            var dir = deltaTo / dist;
            outDesiredDelta = outDesiredDelta + dir * step;
            state.Forward = dir;

            if (step >= dist - 0.00001f)
            {
                path.Index++;
                if (path.Index >= path.Points.Length) path.IsActive = false;
            }
        }
    }
}
