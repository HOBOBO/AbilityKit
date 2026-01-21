using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class SummonMO
    {
        public int Id { get; }
        public string Name { get; }

        public int UnitSubType { get; }
        public int ModelId { get; }

        public int AttributeTemplateId { get; }

        public int LifetimeMs { get; }
        public bool DespawnOnOwnerDie { get; }

        public int MaxAlivePerOwner { get; }
        public int OverflowPolicy { get; }

        public int StatsMode { get; }
        public IReadOnlyList<SummonAttrScaleMO> AttrScales { get; }

        public IReadOnlyList<int> SkillIds { get; }
        public IReadOnlyList<int> PassiveSkillIds { get; }

        public IReadOnlyList<int> Tags { get; }

        public SummonMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.SummonDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name;

            UnitSubType = dto.UnitSubType;
            ModelId = dto.ModelId;

            AttributeTemplateId = dto.AttributeTemplateId;

            LifetimeMs = dto.LifetimeMs;
            DespawnOnOwnerDie = dto.DespawnOnOwnerDie;

            MaxAlivePerOwner = dto.MaxAlivePerOwner;
            OverflowPolicy = dto.OverflowPolicy;

            StatsMode = dto.StatsMode;

            if (dto.AttrScales != null && dto.AttrScales.Length > 0)
            {
                var list = new List<SummonAttrScaleMO>(dto.AttrScales.Length);
                for (int i = 0; i < dto.AttrScales.Length; i++)
                {
                    var s = dto.AttrScales[i];
                    if (s == null) continue;
                    list.Add(new SummonAttrScaleMO(s));
                }
                AttrScales = list;
            }
            else
            {
                AttrScales = Array.Empty<SummonAttrScaleMO>();
            }

            SkillIds = dto.SkillIds ?? Array.Empty<int>();
            PassiveSkillIds = dto.PassiveSkillIds ?? Array.Empty<int>();
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }

    public sealed class SummonAttrScaleMO
    {
        public int AttrId { get; }
        public float Ratio { get; }
        public float Add { get; }

        public SummonAttrScaleMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.SummonAttrScaleDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            AttrId = dto.AttrId;
            Ratio = dto.Ratio;
            Add = dto.Add;
        }
    }
}
