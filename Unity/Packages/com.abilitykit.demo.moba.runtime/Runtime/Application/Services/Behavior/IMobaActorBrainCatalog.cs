using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    /// <summary>
    /// 大脑驱动类型。
    /// </summary>
    public enum MobaBrainDriverKind
    {
        Idle = 0,
        /// <summary>手写决策类（MobaBehaviorDecisions 系列）。</summary>
        Code = 1,
        /// <summary>第三方行为树（BTCore，P2 接入）。</summary>
        BTree = 2,
        /// <summary>分层状态机。</summary>
        Hfsm = 3,
    }

    /// <summary>
    /// 大脑定义：BrainId → 驱动类型 + 驱动器定义键及参数。
    /// </summary>
    public readonly struct MobaActorBrainDefinition
    {
        public readonly int BrainId;
        public readonly MobaBrainDriverKind DriverKind;
        /// <summary>
        /// 驱动器定义键：Code 为 "idle" / "patrol" / "chase"，
        /// BTree 为资源名，Hfsm 为已注册状态机工厂名。
        /// </summary>
        public readonly string DecisionName;
        /// <summary>驱动器主参数：chase 为攻击范围，patrol 为停步距离。</summary>
        public readonly float Param0;

        public MobaActorBrainDefinition(int brainId, MobaBrainDriverKind driverKind, string decisionName, float param0 = 0f)
        {
            BrainId = brainId;
            DriverKind = driverKind;
            DecisionName = decisionName;
            Param0 = param0;
        }
    }

    public interface IMobaActorBrainCatalog : IService
    {
        bool TryGet(int brainId, out MobaActorBrainDefinition definition);
    }

    /// <summary>
    /// 默认大脑目录。
    ///
    /// P1 内置映射（后续从配置表驱动）：
    /// - BrainId 1：chase（近战追击最近敌人）——召唤物/小兵默认
    /// - BrainId 2：patrol（出生点附近往返巡逻）
    ///
    /// 未登记的 BrainId 回退到 Idle（与迁移前行为一致）。
    /// </summary>
    [WorldService(typeof(IMobaActorBrainCatalog))]
    public sealed class MobaActorBrainCatalog : IMobaActorBrainCatalog
    {
        private static readonly Dictionary<int, MobaActorBrainDefinition> s_definitions = new()
        {
            [1] = new MobaActorBrainDefinition(1, MobaBrainDriverKind.Code, "chase", param0: 1.5f),
            [2] = new MobaActorBrainDefinition(2, MobaBrainDriverKind.Code, "patrol", param0: 0.5f),
        };

        public bool TryGet(int brainId, out MobaActorBrainDefinition definition)
        {
            if (brainId > 0 && s_definitions.TryGetValue(brainId, out definition))
            {
                return true;
            }

            definition = new MobaActorBrainDefinition(brainId, MobaBrainDriverKind.Idle, "idle");
            return false;
        }

        public void Dispose()
        {
        }
    }
}
