using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// 脑决策输出应用系统（P1）。
    ///
    /// 执行顺序：BrainTick（决策）→ 本系统（写 MoveInput）→ MotionLocomotionInput（消费）。
    ///
    /// 每帧把运行中 BehaviorRuntime 的 <see cref="IBehaviorOutput.Movement"/> 翻译成
    /// <c>MoveInput(Dx, Dz)</c> 写入对应 Actor：
    /// - 有 Movement：方向 = normalize(目标位置 − 当前位置)，交给 Motion 按速度推进
    /// - 无 Movement（决策未要求移动）：写 (0, 0) 停步
    ///
    /// 只处理带 ActorBrain 的实体——玩家的 MoveInput 由 MobaMoveInputCommandHandler 写入，
    /// 当前英雄不挂 Brain，互不冲突。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.BrainOutputApply, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBrainOutputApplySystem : WorldSystemBase
    {
        private MobaBrainService _brains;
        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBrainOutputApplySystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _brains);
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.ActorBrain));
        }

        protected override void OnExecute()
        {
            if (_brains == null || _group == null) return;

            var entities = _group.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorBrain || !e.hasTransform) continue;

                var instanceId = e.actorBrain.BehaviorInstanceId;
                if (!_brains.TryGetBehavior(instanceId, out var behavior) || behavior == null) continue;
                if (behavior.Phase != BehaviorPhase.Running) continue;

                var movement = behavior.Output.Movement;
                if (movement.HasValue && movement.Value.TargetPosition.HasValue)
                {
                    var targetPos = movement.Value.TargetPosition.Value;
                    var ownerPos = e.transform.Value.Position;
                    var dx = targetPos.X - ownerPos.X;
                    var dz = targetPos.Z - ownerPos.Z;
                    var length = (float)System.Math.Sqrt(dx * dx + dz * dz);
                    if (length > 0.0001f)
                    {
                        WriteMoveInput(e, dx / length, dz / length);
                        continue;
                    }
                }

                WriteMoveInput(e, 0f, 0f);
            }
        }

        private static void WriteMoveInput(global::ActorEntity e, float dx, float dz)
        {
            if (e.hasMoveInput)
            {
                if (System.Math.Abs(e.moveInput.Dx - dx) < 0.0001f
                    && System.Math.Abs(e.moveInput.Dz - dz) < 0.0001f)
                {
                    return;
                }

                e.ReplaceMoveInput(dx, dz);
            }
            else
            {
                e.AddMoveInput(dx, dz);
            }
        }
    }
}
