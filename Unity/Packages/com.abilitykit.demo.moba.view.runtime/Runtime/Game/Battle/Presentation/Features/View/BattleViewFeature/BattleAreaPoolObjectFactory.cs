using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Area VFX 池对象工厂：按 (templateId, kind) 创建 AOE 相关的池化 GameObject。
    /// 从 BattleViewFeature.Lifecycle 提取（2026-07-24），供预测/确认两个 view feature 复用。
    /// </summary>
    internal static class BattleAreaPoolObjectFactory
    {
        public static GameObject Create(
            BattleViewResourceProvider resources,
            int templateId,
            BattleAreaVfxPool.PoolKind kind)
        {
            if (resources == null) return null;

            var aoe = resources.TryGetAoe(templateId);
            switch (kind)
            {
                case BattleAreaVfxPool.PoolKind.Model:
                    return resources.CreateModelGo(templateId);
                case BattleAreaVfxPool.PoolKind.Range:
                    return resources.CreateAoeRangeGo(
                        templateId,
                        aoe != null ? aoe.Radius : 1f,
                        aoe != null ? aoe.DelayMs : 0);
                case BattleAreaVfxPool.PoolKind.Vfx:
                    return resources.CreateVfxGo(aoe != null ? aoe.VfxId : templateId);
                default:
                    return null;
            }
        }
    }
}
