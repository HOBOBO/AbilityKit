using System;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 标记一个可被发现的强类型计划动作模块。
    /// 新增 MOBA 触发动作应继承对应的强类型动作模块基类，并配套对应的动作结构描述。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PlanActionModuleAttribute : Attribute
    {
        public int Order { get; }

        public PlanActionModuleAttribute(int order = 0)
        {
            Order = order;
        }
    }
    /// <summary>
    /// 已发现 MOBA 计划动作模块的集中排序表。
    /// </summary>
    public static class MobaPlanActionModuleOrders
    {
        public const int DebugLog = 0;
        public const int SetGameplayVar = 0;
        public const int AddGameplayVar = 0;
        public const int SetNumericBlackboard = 0;
        public const int AddNumericBlackboard = 0;
        public const int SetBlackboardVariable = 0;
        public const int AdvanceGameplayCounter = 0;
        public const int EndGame = 0;

        public const int CancelSkill = 9;
        public const int ConsumeResource = 10;
        public const int ModifyResource = 10;
        public const int ConvertResourceToHeal = 10;
        public const int StartCooldown = 10;
        public const int ResetCooldown = 10;
        public const int ShootProjectile = 10;
        public const int GiveDamage = 11;
        public const int AdjustDamageNumber = 11;
        public const int TakeDamage = 12;
        public const int Heal = 12;

        public const int Dash = 13;
        public const int Blink = 14;
        public const int RemoveBuff = 14;
        public const int Pull = 15;
        public const int Jump = 16;

        public const int AddShield = 19;
        public const int AddBuff = 20;
        public const int RemoveShield = 20;

        public const int SpawnArea = 24;
        public const int SpawnSummon = 30;
        public const int RemoveSummon = 31;
        public const int RemoveProjectile = 31;
        public const int RemoveArea = 32;

        public const int PlayPresentation = 40;
        public const int Emit = 40;
    }
}
