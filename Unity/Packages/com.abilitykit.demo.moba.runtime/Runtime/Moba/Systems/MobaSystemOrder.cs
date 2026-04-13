using AbilityKit.Ability.World;

namespace AbilityKit.Demo.Moba.Systems
{
    public static class MobaSystemOrder
    {
        public const int Base = WorldSystemOrder.CoreBase + 1000;  // 业务层基准值

        public const int EntityManagerSync = Base + WorldSystemOrder.Early + 5;
        public const int EntityManagerCleanup = Base + WorldSystemOrder.Late + 5;

        public const int ProjectileSync = Base + WorldSystemOrder.Late + 10;
        public const int ProjectileLauncherCleanup = Base + WorldSystemOrder.Late + 12;

        public const int SummonLifecycle = Base + WorldSystemOrder.Late + 14;

        public const int MotionInit = Base + WorldSystemOrder.Early + 10;
        public const int MotionLocomotionInput = Base + WorldSystemOrder.Normal + 50;
        public const int MotionTick = Base + WorldSystemOrder.Late + 20;

        public const int PassiveSkillTriggers = Base + WorldSystemOrder.Normal + 85;
        public const int EffectListeners = Base + WorldSystemOrder.Normal + 90;
        public const int SkillPipelines = Base + WorldSystemOrder.Normal + 100;
        public const int EffectsStep = Base + WorldSystemOrder.Normal + 200;

        public const int BuffCommandsDrain = Base + WorldSystemOrder.Normal + 295;
        public const int BuffsApply = Base + WorldSystemOrder.Normal + 300;
        public const int BuffsRemove = Base + WorldSystemOrder.Normal + 305;
        public const int BuffsTick = Base + WorldSystemOrder.Normal + 310;

        public const int OngoingTriggerPlansReconcile = Base + WorldSystemOrder.Normal + 312;
        public const int OngoingEffectsTick = Base + WorldSystemOrder.Normal + 315;
    }
}