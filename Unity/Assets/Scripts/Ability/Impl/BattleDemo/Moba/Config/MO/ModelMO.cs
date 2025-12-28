using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class ModelMO
    {
        public int Id { get; }
        public string PrefabPath { get; }
        public float Scale { get; }

        public ModelMO(IModelCO co)
        {
            if (co == null) throw new ArgumentNullException(nameof(co));
            Id = co.Key;
            PrefabPath = co.PrefabPath;
            Scale = co.Scale;
        }

        public ModelMO(ModelDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            PrefabPath = dto.PrefabPath;
            Scale = dto.Scale;
        }
    }
}
