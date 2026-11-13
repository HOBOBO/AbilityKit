using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class SkillFlowMO
    {
        public int Id { get; }
        public string Name { get; }
        public int PipelineContinuousTagTemplateId { get; }
        public int ChildChannelId { get; }
        public int RecastChannelId { get; }
        public int RequiredBuffQualificationId { get; }
        public IReadOnlyList<SkillPhaseDTO> Phases { get; }

        public SkillFlowMO(SkillFlowDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            PipelineContinuousTagTemplateId = dto.PipelineContinuousTagTemplateId;
            if (dto.ChildChannelId < 0 || dto.RecastChannelId < 0 || dto.ChildChannelId > 0 && dto.RecastChannelId > 0)
                throw new ArgumentException("Flow child channel and recast channel must be nonnegative and mutually exclusive.", nameof(dto));
            ChildChannelId = dto.ChildChannelId;
            RecastChannelId = dto.RecastChannelId;
            if (dto.RequiredBuffQualificationId < 0) throw new ArgumentException("Required buff qualification must be nonnegative.", nameof(dto));
            RequiredBuffQualificationId = dto.RequiredBuffQualificationId;
            Phases = dto.Phases ?? Array.Empty<SkillPhaseDTO>();
        }
    }
}
