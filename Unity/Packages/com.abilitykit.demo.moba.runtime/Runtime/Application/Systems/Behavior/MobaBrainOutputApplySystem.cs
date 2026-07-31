using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Map;

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
        private SkillCastCoordinator _skills;
        private IFrameTime _frameTime;
        private IMobaMapRuntimeService _maps;
        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaBrainOutputApplySystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _brains);
            Services.TryResolve(out _skills);
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _maps);
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
                var wroteMovement = false;
                if (movement.HasValue && movement.Value.TargetPosition.HasValue)
                {
                    var targetPos = movement.Value.TargetPosition.Value;
                    if (_maps != null && _maps.IsLoaded
                        && _maps.TryProjectToWalkable(in targetPos, 0.5f, out var projectedTarget))
                    {
                        targetPos = projectedTarget;
                    }

                    var ownerPos = e.transform.Value.Position;
                    var dx = targetPos.X - ownerPos.X;
                    var dz = targetPos.Z - ownerPos.Z;
                    var length = (float)System.Math.Sqrt(dx * dx + dz * dz);
                    if (length > 0.0001f)
                    {
                        WriteMoveInput(e, dx / length, dz / length);
                        wroteMovement = true;
                    }
                }

                if (!wroteMovement) WriteMoveInput(e, 0f, 0f);
                ApplySkillEvents(e, behavior.Output.PendingEvents);
            }
        }

        private void ApplySkillEvents(
            global::ActorEntity actor,
            System.Collections.Generic.IReadOnlyList<PendingEvent> events)
        {
            if (_skills == null || events == null || actor == null || !actor.hasActorId) return;

            for (var i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (!string.Equals(evt.EventId, MobaBrainExecutor.SkillCastEventId,
                        System.StringComparison.Ordinal))
                    continue;

                var payload = evt.Payload;
                if (payload == null || !TryGet(payload, MobaBrainExecutor.SkillSlotParam, out int slot) || slot <= 0)
                    continue;
                TryGet(payload, MobaBrainExecutor.SkillIdParam, out int skillId);
                if (!IsSkillReady(actor, slot, skillId)) continue;

                TryGet(payload, MobaBrainExecutor.TargetActorIdParam, out int targetActorId);
                TryGet(payload, MobaBrainExecutor.AimPositionParam, out Vec3 aimPosition);
                if (!TryGet(payload, MobaBrainExecutor.AimDirectionParam, out Vec3 aimDirection))
                    aimDirection = Vec3.Forward;

                _skills.TryCastBySlot(actor.actorId.Value, slot, in aimPosition, in aimDirection, targetActorId);
            }
        }

        private bool IsSkillReady(global::ActorEntity actor, int slot, int expectedSkillId)
        {
            if (!actor.hasSkillLoadout || actor.skillLoadout.ActiveSkills == null) return false;
            var index = slot - 1;
            if (index < 0 || index >= actor.skillLoadout.ActiveSkills.Length) return false;
            var runtime = actor.skillLoadout.ActiveSkills[index];
            if (runtime == null) return false;
            if (expectedSkillId > 0 && runtime.SkillId != expectedSkillId) return false;
            return _frameTime == null || runtime.CooldownEndTimeMs <= MobaSkillRuntimeAccess.GetCurrentTimeMs(_frameTime);
        }

        private static bool TryGet<T>(
            System.Collections.Generic.IReadOnlyDictionary<string, object> payload,
            string key,
            out T value)
        {
            if (payload.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
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
