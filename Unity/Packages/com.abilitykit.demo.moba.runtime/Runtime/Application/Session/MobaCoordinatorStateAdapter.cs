using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Coordinator;

namespace AbilityKit.Demo.Moba.Session
{
    /// <summary>
    /// 将逻辑世界实体状态快照转换为面向 coordinator 的状态记录。
    /// </summary>
    public sealed class MobaCoordinatorStateAdapter
    {
        public SnapshotEntityState[] ToCoordinatorStates(LogicWorldEntityState[] states)
        {
            if (states == null || states.Length == 0)
            {
                return Array.Empty<SnapshotEntityState>();
            }

            var result = new SnapshotEntityState[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i];
                var entityState = new EntityState
                {
                    EntityId = state.EntityId,
                    X = state.X,
                    Y = state.Y,
                    Z = state.Z,
                    Rotation = state.Rotation,
                    VelocityX = state.VelocityX,
                    VelocityZ = state.VelocityZ,
                    Hp = state.Hp,
                    HpMax = state.HpMax,
                    TeamId = state.TeamId,
                    IsDead = state.IsDead
                };
                result[i] = entityState.ToSnapshotEntityState();
            }

            return result;
        }

        public int AppendToCoordinatorStates(
            IList<LogicWorldEntityState> states,
            IList<SnapshotEntityState> destination)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var entityState = new EntityState
                {
                    EntityId = state.EntityId,
                    X = state.X,
                    Y = state.Y,
                    Z = state.Z,
                    Rotation = state.Rotation,
                    VelocityX = state.VelocityX,
                    VelocityZ = state.VelocityZ,
                    Hp = state.Hp,
                    HpMax = state.HpMax,
                    TeamId = state.TeamId,
                    IsDead = state.IsDead
                };
                destination.Add(entityState.ToSnapshotEntityState());
            }

            return states.Count;
        }
    }
}
