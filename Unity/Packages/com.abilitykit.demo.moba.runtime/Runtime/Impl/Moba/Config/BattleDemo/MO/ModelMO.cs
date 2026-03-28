using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public sealed class ModelMO
    {
        public int Id { get; }
        public string PrefabPath { get; }
        public float Scale { get; }

        public ModelMO(ModelDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            PrefabPath = dto.PrefabPath;
            Scale = dto.Scale;
        }
    }
}
