using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class SkillMO
    {
        public int Id { get; }
        public string Name { get; }
        public int CooldownMs { get; }
        public int Range { get; }
        public int IconId { get; }
        public int Category { get; }
        public SkillType SkillType { get; }
        public int CastConflictGroup { get; }
        public int InterruptPriority { get; }
        public bool InterruptRunning { get; }
        public bool Uninterruptible { get; }
        public int TargetLostPolicy { get; }
        public int SkillButtonTemplateId { get; }
        public int RequiredTargetQueryId { get; }
        public int LevelTableId { get; }
        public int PreCastFlowId { get; }
        public int CastFlowId { get; }
        public IReadOnlyList<int> Tags { get; }

        public SkillMO(SkillDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            CooldownMs = dto.CooldownMs;
            Range = dto.Range;
            IconId = dto.IconId;
            Category = dto.Category;
            SkillType = (SkillType)dto.SkillType;
            CastConflictGroup = dto.CastConflictGroup;
            InterruptPriority = dto.InterruptPriority;
            InterruptRunning = dto.InterruptRunning;
            Uninterruptible = dto.Uninterruptible;
            TargetLostPolicy = dto.TargetLostPolicy;
            SkillButtonTemplateId = dto.SkillButtonTemplateId;
            RequiredTargetQueryId = dto.RequiredTargetQueryId;
            LevelTableId = dto.LevelTableId;
            PreCastFlowId = dto.PreCastFlowId;
            CastFlowId = dto.CastFlowId;
            Tags = dto.Tags ?? Array.Empty<int>();
        }
    }
}
