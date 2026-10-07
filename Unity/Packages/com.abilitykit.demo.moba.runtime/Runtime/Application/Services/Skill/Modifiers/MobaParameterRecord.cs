using System;

namespace AbilityKit.Demo.Moba.Services
{
    [Flags]
    public enum MobaParameterIdentityFields
    {
        None = 0,
        Slot = 1 << 0,
        BaseSkillId = 1 << 1,
        EffectiveSkillId = 1 << 2,
        LauncherTemplateId = 1 << 3,
        ProjectileTemplateId = 1 << 4,
        AreaTemplateId = 1 << 5,
        SummonTemplateId = 1 << 6,
        BuffTemplateId = 1 << 7,
    }

    public enum MobaParameterStage
    {
        None = 0,
        CastPrepare = 1,
        LauncherCreate = 2,
        Emit = 3,
        ProjectileSpawn = 4,
        Hit = 5,
        AreaSpawn = 6,
        SummonCreate = 7,
        BuffApply = 8,
    }

    // Zero means that a field is unavailable. Selectors cannot match unavailable fields.
    public readonly struct MobaParameterRecord
    {
        public static MobaParameterRecord FromSkillRuntime(MobaParameterStage stage,
            MobaSkillCastRuntimeService runtimes, in MobaSkillCastRuntimeHandle handle,
            int launcherTemplateId = 0, int projectileTemplateId = 0,
            int areaTemplateId = 0, int summonTemplateId = 0, int buffTemplateId = 0)
        {
            if (runtimes != null && handle.IsValid && runtimes.TryGet(in handle, out var runtime) && runtime != null)
                return new MobaParameterRecord(stage, runtime.SkillSlot,
                    baseSkillId: runtime.BaseSkillId, effectiveSkillId: runtime.SkillId,
                    launcherTemplateId: launcherTemplateId, projectileTemplateId: projectileTemplateId,
                    areaTemplateId: areaTemplateId, summonTemplateId: summonTemplateId,
                    buffTemplateId: buffTemplateId);
            return new MobaParameterRecord(stage, launcherTemplateId: launcherTemplateId,
                projectileTemplateId: projectileTemplateId, areaTemplateId: areaTemplateId,
                summonTemplateId: summonTemplateId, buffTemplateId: buffTemplateId);
        }

        public MobaParameterRecord(MobaParameterStage stage, int slot = 0, int baseSkillId = 0,
            int effectiveSkillId = 0, int launcherTemplateId = 0, int projectileTemplateId = 0,
            int areaTemplateId = 0, int summonTemplateId = 0, int buffTemplateId = 0)
        {
            Stage = stage;
            Slot = slot;
            BaseSkillId = baseSkillId;
            EffectiveSkillId = effectiveSkillId;
            LauncherTemplateId = launcherTemplateId;
            ProjectileTemplateId = projectileTemplateId;
            AreaTemplateId = areaTemplateId;
            SummonTemplateId = summonTemplateId;
            BuffTemplateId = buffTemplateId;
        }

        public MobaParameterStage Stage { get; }
        public int Slot { get; }
        public int BaseSkillId { get; }
        public int EffectiveSkillId { get; }
        public int LauncherTemplateId { get; }
        public int ProjectileTemplateId { get; }
        public int AreaTemplateId { get; }
        public int SummonTemplateId { get; }
        public int BuffTemplateId { get; }

    }

    public readonly struct MobaParameterSelector
    {
        public MobaParameterSelector(int slot = 0, int baseSkillId = 0, int effectiveSkillId = 0,
            int launcherTemplateId = 0, int projectileTemplateId = 0, int areaTemplateId = 0,
            int summonTemplateId = 0, int buffTemplateId = 0)
        {
            Slot = slot;
            BaseSkillId = baseSkillId;
            EffectiveSkillId = effectiveSkillId;
            LauncherTemplateId = launcherTemplateId;
            ProjectileTemplateId = projectileTemplateId;
            AreaTemplateId = areaTemplateId;
            SummonTemplateId = summonTemplateId;
            BuffTemplateId = buffTemplateId;
        }

        public int Slot { get; }
        public int BaseSkillId { get; }
        public int EffectiveSkillId { get; }
        public int LauncherTemplateId { get; }
        public int ProjectileTemplateId { get; }
        public int AreaTemplateId { get; }
        public int SummonTemplateId { get; }
        public int BuffTemplateId { get; }

        public bool IsEmpty => Slot == 0 && BaseSkillId == 0 && EffectiveSkillId == 0 &&
            LauncherTemplateId == 0 && ProjectileTemplateId == 0 && AreaTemplateId == 0 &&
            SummonTemplateId == 0 && BuffTemplateId == 0;

        public bool IsValidForStage(MobaParameterStage stage)
        {
            switch (stage)
            {
                case MobaParameterStage.CastPrepare:
                    return LauncherTemplateId == 0 && ProjectileTemplateId == 0 &&
                           AreaTemplateId == 0 && SummonTemplateId == 0 && BuffTemplateId == 0;
                case MobaParameterStage.LauncherCreate:
                case MobaParameterStage.Emit:
                case MobaParameterStage.ProjectileSpawn:
                case MobaParameterStage.Hit:
                    return AreaTemplateId == 0 && SummonTemplateId == 0 && BuffTemplateId == 0;
                case MobaParameterStage.AreaSpawn:
                    return LauncherTemplateId == 0 &&
                           SummonTemplateId == 0 && BuffTemplateId == 0;
                case MobaParameterStage.SummonCreate:
                    return LauncherTemplateId == 0 && ProjectileTemplateId == 0 &&
                           AreaTemplateId == 0 && BuffTemplateId == 0;
                case MobaParameterStage.BuffApply:
                    return LauncherTemplateId == 0 &&
                           AreaTemplateId == 0 && SummonTemplateId == 0;
                default:
                    return IsEmpty;
            }
        }

        public bool Matches(in MobaParameterRecord record)
        {
            return (Slot == 0 || Slot == record.Slot) &&
                   (BaseSkillId == 0 || BaseSkillId == record.BaseSkillId) &&
                   (EffectiveSkillId == 0 || EffectiveSkillId == record.EffectiveSkillId) &&
                   (LauncherTemplateId == 0 || LauncherTemplateId == record.LauncherTemplateId) &&
                   (ProjectileTemplateId == 0 || ProjectileTemplateId == record.ProjectileTemplateId) &&
                   (AreaTemplateId == 0 || AreaTemplateId == record.AreaTemplateId) &&
                   (SummonTemplateId == 0 || SummonTemplateId == record.SummonTemplateId) &&
                   (BuffTemplateId == 0 || BuffTemplateId == record.BuffTemplateId);
        }

        public MobaParameterIdentityFields MissingFields(in MobaParameterRecord record)
        {
            var missing = MobaParameterIdentityFields.None;
            if (Slot != 0 && record.Slot == 0) missing |= MobaParameterIdentityFields.Slot;
            if (BaseSkillId != 0 && record.BaseSkillId == 0) missing |= MobaParameterIdentityFields.BaseSkillId;
            if (EffectiveSkillId != 0 && record.EffectiveSkillId == 0) missing |= MobaParameterIdentityFields.EffectiveSkillId;
            if (LauncherTemplateId != 0 && record.LauncherTemplateId == 0) missing |= MobaParameterIdentityFields.LauncherTemplateId;
            if (ProjectileTemplateId != 0 && record.ProjectileTemplateId == 0) missing |= MobaParameterIdentityFields.ProjectileTemplateId;
            if (AreaTemplateId != 0 && record.AreaTemplateId == 0) missing |= MobaParameterIdentityFields.AreaTemplateId;
            if (SummonTemplateId != 0 && record.SummonTemplateId == 0) missing |= MobaParameterIdentityFields.SummonTemplateId;
            if (BuffTemplateId != 0 && record.BuffTemplateId == 0) missing |= MobaParameterIdentityFields.BuffTemplateId;
            return missing;
        }
    }

    public interface IMobaContinuousParameterSelectorSpec
    {
        MobaParameterSelector Selector { get; }
    }
}
