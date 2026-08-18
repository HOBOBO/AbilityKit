using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Input;

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
        private MobaCombatRulesService _combatRules;
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
            Services.TryResolve(out _combatRules);
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
                // A zero id denotes a non-Behavior controller (currently actor HFSM). It owns its
                // input after ActorStateMachineTick; BrainService clears stale input on switching.
                if (instanceId <= 0) continue;
                if (!_brains.TryGetBehavior(instanceId, out var behavior) || behavior == null
                    || behavior.Phase != BehaviorPhase.Running)
                {
                    WriteMoveInput(e, 0f, 0f);
                    continue;
                }

                var intent = MobaBrainIntentReader.Read(behavior.Output);
                var wroteMovement = false;
                var canMove = intent.IsValid()
                    && (_combatRules == null || _combatRules.CanMove(e.actorId.Value));
                if (canMove && intent.MovementKind == MobaActorMovementIntentKind.Direction)
                {
                    WriteMoveInput(e, intent.MoveX, intent.MoveZ);
                    wroteMovement = System.Math.Abs(intent.MoveX) > 0.0001f
                        || System.Math.Abs(intent.MoveZ) > 0.0001f;
                }
                else if (canMove && intent.MovementKind == MobaActorMovementIntentKind.TargetPosition)
                {
                    var targetPos = intent.MoveTarget;
                    if (_maps != null && _maps.IsLoaded
                        && _maps.TryProjectToWalkable(in targetPos, 0.5f, out var projectedTarget))
                    {
                        targetPos = projectedTarget;
                    }

                    var ownerPos = e.transform.Value.Position;
                    var dx = targetPos.X - ownerPos.X;
                    var dz = targetPos.Z - ownerPos.Z;
                    var length = DeterministicMathBridge.Sqrt(dx * dx + dz * dz);
                    if (length > 0.0001f)
                    {
                        WriteMoveInput(e, dx / length, dz / length);
                        wroteMovement = true;
                    }
                }

                if (!wroteMovement) WriteMoveInput(e, 0f, 0f);
                ApplySkillIntent(e, in intent);
            }
        }

        private void ApplySkillIntent(global::ActorEntity actor, in MobaActorIntent intent)
        {
            if (_skills == null || actor == null || !actor.hasActorId || !intent.IsValid() || !intent.HasCast
                || intent.SkillSlot <= 0 || !IsSkillReady(actor, intent.SkillSlot, intent.SkillId)
                || (_combatRules != null && !_combatRules.CanCastSkill(actor.actorId.Value).Passed))
                return;

            var aimPosition = intent.AimPosition;
            var aimDirection = intent.AimDirection;
            _skills.TryCastBySlot(
                actor.actorId.Value,
                intent.SkillSlot,
                in aimPosition,
                in aimDirection,
                intent.TargetActorId);
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
