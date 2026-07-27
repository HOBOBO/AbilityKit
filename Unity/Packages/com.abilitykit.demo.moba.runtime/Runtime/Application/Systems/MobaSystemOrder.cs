using AbilityKit.Ability.World;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// MOBA 战斗逻辑系统执行顺序常量。
    ///
    /// 此文件定义 moba.runtime 的正式系统执行顺序。
    ///
    /// 设计原则:
    /// - Base = WorldSystemOrder.CoreBase + 1000 (业务层基准)
    /// - 使用 WorldSystemPhase 的偏移量 (Early, Normal, Late)
    /// - 每个功能模块预留 10-20 的偏移空间
    ///
    /// 参考文档: Docs/SystemOrderGuide.md
    /// </summary>
    public static class MobaSystemOrder
    {
        public readonly struct OrderCheckResult
        {
            public OrderCheckResult(bool passed, string message)
            {
                Passed = passed;
                Message = message;
            }

            public bool Passed { get; }
            public string Message { get; }
        }

        // ========== 基准值 ==========
        /// <summary>
        /// 业务层基准值
        /// 所有业务系统以此为基准计算执行顺序
        /// </summary>
        public const int Base = WorldSystemOrder.CoreBase + 1000;

        // ========== 实体管理层 (PreExecute/Early) ==========
        /// <summary>实体管理器同步</summary>
        public const int EntityManagerSync = Base + WorldSystemOrder.Early + 5;
        /// <summary>移动系统初始化</summary>
        public const int MotionInit = Base + WorldSystemOrder.Early + 10;

        // ========== 移动系统 (Execute/Normal) ==========
        /// <summary>Actor 脑决策 Tick</summary>
        public const int BrainTick = Base + WorldSystemOrder.Normal + 8;
        /// <summary>脑决策输出应用（Movement → MoveInput）</summary>
        public const int BrainOutputApply = Base + WorldSystemOrder.Normal + 9;
        /// <summary>移动输入处理</summary>
        public const int MotionLocomotionInput = Base + WorldSystemOrder.Normal + 10;
        /// <summary>移动 Tick</summary>
        public const int MotionTick = Base + WorldSystemOrder.Normal + 50;

        // ========== 技能系统 (Execute/Normal) ==========
        /// <summary>被动技能触发注册</summary>
        public const int PassiveSkillTriggers = Base + WorldSystemOrder.Normal + 85;
        /// <summary>效果监听器</summary>
        public const int EffectListeners = Base + WorldSystemOrder.Normal + 90;
        /// <summary>技能管道执行</summary>
        public const int SkillPipelines = Base + WorldSystemOrder.Normal + 100;

        // ========== 战斗系统 (Execute/Normal) ==========
        /// <summary>效果步骤</summary>
        public const int EffectsStep = Base + WorldSystemOrder.Normal + 200;

        // ========== Buff 系统 (Execute/Normal) ==========
        /// <summary>Buff 命令队列处理</summary>
        public const int BuffCommandsDrain = Base + WorldSystemOrder.Normal + 295;

        // ========== 持续效果系统 (Execute/Normal) ==========
        /// <summary>持续行为统一 Tick</summary>
        public const int ContinuousTick = Base + WorldSystemOrder.Normal + 307;
        /// <summary>Buff 生命周期调和</summary>
        public const int BuffLifecycleReconcile = Base + WorldSystemOrder.Normal + 310;
        /// <summary>持续触发器计划调和</summary>
        public const int OngoingTriggerPlansReconcile = Base + WorldSystemOrder.Normal + 312;
        /// <summary>玩法规则 Tick</summary>
        public const int GameplayTick = Base + WorldSystemOrder.Normal + 320;

        // ========== 清理系统 (PostExecute/Late) ==========
        /// <summary>实体管理器清理</summary>
        public const int EntityManagerCleanup = Base + WorldSystemOrder.Late + 5;
        /// <summary>投射物同步</summary>
        public const int ProjectileSync = Base + WorldSystemOrder.Late + 10;
        /// <summary>投射物发射器清理</summary>
        public const int ProjectileLauncherCleanup = Base + WorldSystemOrder.Late + 12;
        /// <summary>护盾生命周期</summary>
        public const int ShieldLifecycle = Base + WorldSystemOrder.Late + 13;
        /// <summary>召唤物生命周期</summary>
        public const int SummonLifecycle = Base + WorldSystemOrder.Late + 14;
        /// <summary>Actor 统一移除清理</summary>
        public const int ActorDespawnCleanup = Base + WorldSystemOrder.Late + 18;

        // ========== 诊断系统 (PostExecute/Late) ==========
        /// <summary>诊断状态采样：在所有业务系统和清理系统之后采样当前帧 World/Actor 状态快照</summary>
        public const int DiagnosticStateSample = Base + WorldSystemOrder.Late + 30;

        public static OrderCheckResult ValidateKeyDependencies()
        {
            if (!RunsBefore(EntityManagerSync, MotionInit))
            {
                return Fail(nameof(EntityManagerSync), nameof(MotionInit));
            }

            if (!RunsBefore(BrainTick, MotionLocomotionInput))
            {
                return Fail(nameof(BrainTick), nameof(MotionLocomotionInput));
            }

            if (!RunsBefore(MotionLocomotionInput, MotionTick))
            {
                return Fail(nameof(MotionLocomotionInput), nameof(MotionTick));
            }

            if (!RunsBefore(PassiveSkillTriggers, SkillPipelines))
            {
                return Fail(nameof(PassiveSkillTriggers), nameof(SkillPipelines));
            }

            if (!RunsBefore(SkillPipelines, EffectsStep))
            {
                return Fail(nameof(SkillPipelines), nameof(EffectsStep));
            }

            if (!RunsBefore(EffectsStep, BuffCommandsDrain))
            {
                return Fail(nameof(EffectsStep), nameof(BuffCommandsDrain));
            }

            if (!RunsBefore(BuffCommandsDrain, ContinuousTick))
            {
                return new OrderCheckResult(false, "Buff order must be BuffCommandsDrain < ContinuousTick.");
            }

            if (!RunsBefore(ContinuousTick, BuffLifecycleReconcile) || !RunsBefore(BuffLifecycleReconcile, OngoingTriggerPlansReconcile) || !RunsBefore(OngoingTriggerPlansReconcile, GameplayTick))
            {
                return new OrderCheckResult(false, "Continuous order must be ContinuousTick < BuffLifecycleReconcile < OngoingTriggerPlansReconcile < GameplayTick.");
            }

            if (!RunsBefore(ProjectileSync, ProjectileLauncherCleanup) || !RunsBefore(ProjectileLauncherCleanup, ShieldLifecycle) || !RunsBefore(ShieldLifecycle, SummonLifecycle) || !RunsBefore(SummonLifecycle, ActorDespawnCleanup))
            {
                return new OrderCheckResult(false, "Late cleanup order must be ProjectileSync < ProjectileLauncherCleanup < ShieldLifecycle < SummonLifecycle < ActorDespawnCleanup.");
            }

            if (!RunsBefore(ActorDespawnCleanup, DiagnosticStateSample))
            {
                return new OrderCheckResult(false, "DiagnosticStateSample must run after ActorDespawnCleanup.");
            }

            return new OrderCheckResult(true, null);
        }

        private static bool RunsBefore(int earlier, int later)
        {
            return earlier < later;
        }

        private static OrderCheckResult Fail(string earlier, string later)
        {
            return new OrderCheckResult(false, $"{earlier} must run before {later}.");
        }
    }
}
