using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Events
{
    /// <summary>
    /// 【模板】游戏事件常量
    ///
    /// 此文件定义了游戏事件常量。
    /// 遵循 "{子系统}.{动作}" 命名规范。
    ///
    /// 新建游戏世界层时应参考此模板定义自己的事件常量。
    ///
    /// 参考文档: Docs/EventGuide.md
    /// </summary>
    public static class GameEvents
    {
        // ========== 战斗事件 ==========
        /// <summary>伤害创建</summary>
        public const string Combat_AttackCreated = "combat.attack_created";
        /// <summary>伤害计算前</summary>
        public const string Combat_BeforeCalc = "combat.before_calc";
        /// <summary>伤害计算开始</summary>
        public const string Combat_CalcBegin = "combat.calc_begin";
        /// <summary>伤害计算后（基础抗性）</summary>
        public const string Combat_AfterBase = "combat.after_base";
        /// <summary>伤害计算后（减伤）</summary>
        public const string Combat_AfterMitigate = "combat.after_mitigate";
        /// <summary>伤害计算后（护盾）</summary>
        public const string Combat_AfterShield = "combat.after_shield";
        /// <summary>伤害计算最终</summary>
        public const string Combat_CalcFinal = "combat.calc_final";
        /// <summary>伤害应用前</summary>
        public const string Combat_BeforeApply = "combat.before_apply";
        /// <summary>伤害应用后</summary>
        public const string Combat_AfterApply = "combat.after_apply";

        // ========== 技能事件 ==========
        /// <summary>技能开始释放</summary>
        public const string Skill_CastStart = "skill.cast_start";
        /// <summary>技能释放成功</summary>
        public const string Skill_Cast = "skill.cast";
        /// <summary>技能释放失败</summary>
        public const string Skill_CastFail = "skill.cast_fail";
        /// <summary>技能冷却开始</summary>
        public const string Skill_CooldownStart = "skill.cooldown_start";
        /// <summary>技能冷却结束</summary>
        public const string Skill_CooldownEnd = "skill.cooldown_end";

        // ========== Buff 事件 ==========
        /// <summary>Buff 应用</summary>
        public const string Buff_Apply = MobaBuffTriggering.Events.Apply;
        /// <summary>Buff 叠加</summary>
        public const string Buff_Stack = MobaBuffTriggering.Events.Stack;
        /// <summary>Buff 刷新</summary>
        public const string Buff_Refresh = MobaBuffTriggering.Events.Refresh;
        /// <summary>Buff 移除</summary>
        public const string Buff_Remove = MobaBuffTriggering.Events.Remove;
        /// <summary>Buff Tick</summary>
        public const string Buff_Tick = MobaBuffTriggering.Events.Tick;
        /// <summary>Buff 结束</summary>
        public const string Buff_End = MobaBuffTriggering.Events.End;

        // ========== 投射物事件 ==========
        /// <summary>投射物生成</summary>
        public const string Projectile_Spawn = "projectile.spawn";
        /// <summary>投射物飞行</summary>
        public const string Projectile_Tick = "projectile.tick";
        /// <summary>投射物击中</summary>
        public const string Projectile_Hit = "projectile.hit";
        /// <summary>投射物离开</summary>
        public const string Projectile_Exit = "projectile.exit";

        // ========== 单位事件 ==========
        /// <summary>单位死亡</summary>
        public const string Unit_Die = "unit.die";
        /// <summary>单位重生</summary>
        public const string Unit_Reborn = "unit.reborn";
        /// <summary>单位出生</summary>
        public const string Unit_Spawn = "unit.spawn";
        /// <summary>单位移动</summary>
        public const string Unit_Move = "unit.move";

        // ========== 召唤物事件 ==========
        /// <summary>召唤物出生</summary>
        public const string Summon_Spawn = "summon.spawn";
        /// <summary>召唤物死亡</summary>
        public const string Summon_Die = "summon.die";

        // ========== 区域事件 ==========
        /// <summary>区域进入</summary>
        public const string Area_Enter = "area.enter";
        public const string Area_Cross = "area.cross";
        /// <summary>区域退出</summary>
        public const string Area_Exit = "area.exit";
        /// <summary>区域 Tick</summary>
        public const string Area_Tick = "area.tick";
    }
}
