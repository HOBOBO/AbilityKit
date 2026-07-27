using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    /// <summary>
    /// 召唤物继承属性配置表条目：
    /// 召唤物生成时按 施法者属性 × Ratio + Add 写入自身属性基础值。
    /// 召唤物表通过 InheritAttributeConfigId 引用本表条目。
    /// </summary>
    public sealed class SummonAttrInheritMO
    {
        public int Id { get; }
        public string Name { get; }
        public IReadOnlyList<SummonAttrScaleMO> Scales { get; }

        public SummonAttrInheritMO(SummonAttrInheritDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name ?? string.Empty;

            if (dto.Scales != null && dto.Scales.Length > 0)
            {
                var list = new List<SummonAttrScaleMO>(dto.Scales.Length);
                for (int i = 0; i < dto.Scales.Length; i++)
                {
                    var s = dto.Scales[i];
                    if (s == null) continue;
                    list.Add(new SummonAttrScaleMO(s));
                }
                Scales = list;
            }
            else
            {
                Scales = Array.Empty<SummonAttrScaleMO>();
            }
        }
    }
}
