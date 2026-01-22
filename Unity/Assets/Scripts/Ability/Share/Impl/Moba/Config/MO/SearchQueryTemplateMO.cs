using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class SearchQueryTemplateMO
    {
        public int Id { get; }
        public string Name { get; }

        public int CenterMode { get; }
        public float Radius { get; }
        public int MaxCount { get; }
        public bool ExcludeCaster { get; }

        public SearchQueryTemplateMO(SearchQueryTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            CenterMode = dto.CenterMode;
            Radius = dto.Radius;
            MaxCount = dto.MaxCount;
            ExcludeCaster = dto.ExcludeCaster;
        }
    }
}
