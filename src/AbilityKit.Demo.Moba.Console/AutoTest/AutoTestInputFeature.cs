using System;
using AbilityKit.Demo.Moba.Console.Battle.Context;
using AbilityKit.Demo.Moba.Console.Battle.Input;
using AbilityKit.Demo.Moba.Console.Battle.Flow;
using AbilityKit.Demo.Moba.Console.Platform;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Console.AutoTest
{
    /// <summary>
    /// 自动测试输入特征模块。
    /// 共享 BattleTestScriptRunner 负责脚本步骤与持续 tick 语义，本类型只负责把单个共享 step 映射到 Console HUD 输入。
    /// </summary>
    public sealed class AutoTestInputFeature : IInputFeature, IGameModule<ConsoleBattleContext>
    {
        private ConsoleBattleContext _ctx;
        private bool _initialized;
        private bool _started;

        public int LocalActorId => _ctx?.LocalActorId ?? 0;

        /// <summary>
        /// 开始自动测试输入映射。
        /// </summary>
        public void Start()
        {
            _started = true;
            Log.System("[AutoTest] Auto test started");
        }

        /// <summary>
        /// 停止自动测试输入映射。
        /// </summary>
        public void Stop()
        {
            if (!_started) return;

            _started = false;
            SetMove(0, 0);
            Log.System("[AutoTest] Auto test stopped");
        }

        public void OnAttach(ConsoleBattleContext context)
        {
            _ctx = context ?? throw new ArgumentNullException(nameof(context));
            _initialized = true;
            Log.Trace($"[AutoTest] Attached - PlayerId: {_ctx.LocalActorId}");
        }

        public void OnDetach(ConsoleBattleContext context)
        {
            Stop();
            _ctx = null;
            _initialized = false;
            Log.Trace("[AutoTest] Detached");
        }

        public void Apply(BattleTestStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (!_initialized || !_started || _ctx == null) return;

            switch (step.Kind)
            {
                case BattleTestStepKind.Move:
                    ApplyIntent(MobaActorIntent.MoveDirection(step.Dx, step.Dz));
                    break;
                case BattleTestStepKind.Skill:
                    ApplyIntent(MobaActorIntent.Cast(step.Slot));
                    break;
                case BattleTestStepKind.Wait:
                    break;
                case BattleTestStepKind.Idle:
                    ApplyIntent(MobaActorIntent.Hold);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step.Kind, "Unsupported battle test step kind.");
            }
        }

        private void SetMove(float dx, float dz)
        {
            if (_ctx == null) return;
            _ctx.HudMoveDx = dx;
            _ctx.HudMoveDz = dz;
            _ctx.HudHasMove = Math.Abs(dx) > 0.01f || Math.Abs(dz) > 0.01f;
        }

        #region IInputFeature 实现

        public void ApplyIntent(in MobaActorIntent intent)
        {
            if (!intent.HasFiniteMovement())
            {
                SetMove(0f, 0f);
                return;
            }
            if (intent.MovementKind == MobaActorMovementIntentKind.Direction)
                SetMove(intent.MoveX, intent.MoveZ);
            else if (intent.MovementKind == MobaActorMovementIntentKind.Hold)
                SetMove(0f, 0f);
            if (intent.HasCast && intent.SkillSlot > 0) ClickSkill(intent.SkillSlot);
        }

        public void SetMoveInput(float dx, float dz)
        {
            SetMove(dx, dz);
        }

        public void ClickSkill(int slot)
        {
            if (_ctx == null) return;
            _ctx.HudSkillClickSlot = slot;
        }

        public void AimSkill(int slot, float dx, float dz)
        {
            // Console 自动测试当前只需要点击式技能输入；瞄准式输入后续应扩展共享 BattleTestStep，而不是恢复本地脚本模型。
        }

        public void ReleaseSkillAim(int slot, float dx, float dz)
        {
            // Console 自动测试当前只需要点击式技能输入；瞄准式输入后续应扩展共享 BattleTestStep，而不是恢复本地脚本模型。
        }

        #endregion
    }
}
