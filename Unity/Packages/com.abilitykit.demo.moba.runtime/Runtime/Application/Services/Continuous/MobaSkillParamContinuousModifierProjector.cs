using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaSkillParamContinuousModifierProjector : IMobaContinuousModifierProjector
    {
        private MobaSkillParamModifierService _skillParams;

        public int TargetKind => MobaContinuousModifierTargetKind.SkillParameter;

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _skillParams);
        }

        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection, IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (_skillParams == null) return;
            if (continuous == null || projection == null || modifiers == null || modifiers.Count == 0) return;
            if (projection.OwnerActorId <= 0 || projection.ModifierSourceId == 0) return;

            var owner = MobaModifierOwnerRef.Actor(projection.OwnerActorId);
            if (!owner.IsValid) return;

            var stack = GetStack(continuous.Config);
            for (int i = 0; i < modifiers.Count; i++)
            {
                var spec = modifiers[i];
                if (!CanProject(spec)) continue;

                _skillParams.AddModifier(owner, CreateModifierData(spec, projection, stack));
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (_skillParams == null || projection == null) return;
            if (projection.OwnerActorId <= 0 || projection.ModifierSourceId == 0) return;

            var owner = MobaModifierOwnerRef.Actor(projection.OwnerActorId);
            if (owner.IsValid)
            {
                _skillParams.ClearSource(owner, projection.ModifierSourceId);
            }
        }

        private static ModifierData CreateModifierData(IMobaContinuousModifierSpec spec, IMobaContinuousProjectionConfig projection, int stack)
        {
            var sourceId = projection?.ModifierSourceId ?? 0;
            return new ModifierData
            {
                Key = ToSkillParamKey(spec.TargetId),
                Op = MobaContinuousModifierMath.ToModifierOp(spec.Op),
                Magnitude = ApplyStack(spec.Magnitude, stack),
                Priority = spec.Priority,
                SourceId = sourceId,
                SourceNameIndex = -1,
                Metadata = ModifierMetadata.CreateByIndex(-1, 0, sourceId)
            };
        }

        private static ModifierKey ToSkillParamKey(int targetId)
        {
            switch (targetId)
            {
                case 1: return MobaSkillParamModifierKeys.Projectile.LauncherId;
                case 2: return MobaSkillParamModifierKeys.Projectile.ProjectileId;
                case 3: return MobaSkillParamModifierKeys.Projectile.CountPerShot;
                case 4: return MobaSkillParamModifierKeys.Projectile.FanAngleDeg;
                case 5: return MobaSkillParamModifierKeys.Projectile.DurationMs;
                case 6: return MobaSkillParamModifierKeys.Skill.SkillId;
                case 7: return MobaSkillParamModifierKeys.Summon.SummonId;
                default: return ModifierKey.Create(ModifierKey.Categories.Projectile, ToByte(targetId));
            }
        }

        private static MagnitudeSource ApplyStack(MagnitudeSource magnitude, int stack)
        {
            if (stack <= 1) return magnitude;
            return magnitude.WithBaseValue(magnitude.BaseValue * stack);
        }

        private static bool CanProject(IMobaContinuousModifierSpec spec)
        {
            return spec != null &&
                   spec.TargetKind == MobaContinuousModifierTargetKind.SkillParameter &&
                   spec.TargetId != 0;
        }

        private static int GetStack(IContinuousConfig config)
        {
            return config is IStackConfig stackConfig && stackConfig.Stack > 1 ? stackConfig.Stack : 1;
        }

        private static byte ToByte(int value)
        {
            if (value <= 0) return 0;
            if (value >= byte.MaxValue) return byte.MaxValue;
            return (byte)value;
        }
    }
}
