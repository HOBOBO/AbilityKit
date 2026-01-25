using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class TagTemplateMO
    {
        public int Id { get; }
        public string Name { get; }

        public IReadOnlyList<int> RequiredTags { get; }
        public IReadOnlyList<int> BlockedTags { get; }

        public IReadOnlyList<int> GrantTags { get; }
        public IReadOnlyList<int> RemoveTags { get; }

        public TagTemplateMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.TagTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            RequiredTags = dto.RequiredTags ?? Array.Empty<int>();
            BlockedTags = dto.BlockedTags ?? Array.Empty<int>();
            GrantTags = dto.GrantTags ?? Array.Empty<int>();
            RemoveTags = dto.RemoveTags ?? Array.Empty<int>();
        }
    }
}
