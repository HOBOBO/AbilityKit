using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class GameplayMO
    {
        public int Id { get; }
        public int MapId { get; }
        public string Name { get; }
        public IReadOnlyList<int> TriggerIds { get; }
        public IReadOnlyList<string> FeatureIds { get; }
        public int DefaultDurationMs { get; }
        public IReadOnlyList<int> Tags { get; }

        public GameplayMO(GameplayDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            MapId = dto.MapId;
            Name = dto.Name;
            TriggerIds = dto.TriggerIds ?? Array.Empty<int>();
            FeatureIds = dto.FeatureIds ?? Array.Empty<string>();
            DefaultDurationMs = dto.DefaultDurationMs;
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }
}
