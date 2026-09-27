using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillCastPolicyResolver
    {
        private readonly IWorldResolver _services;
        private MobaConfigDatabase _configs;
        private bool _configResolved;

        public SkillCastPolicyResolver(IWorldResolver services)
        {
            _services = services;
        }

        public SkillCastPolicy Resolve(int skillId, in SkillCastPolicy fallback)
        {
            if (skillId <= 0) return fallback;

            var configs = ResolveConfigs();
            if (configs == null) return fallback;
            if (!configs.TryGetSkill(skillId, out var skill) || skill == null) return fallback;

            return ResolveFromSkill(skill, in fallback);
        }

        private SkillCastPolicy ResolveFromSkill(AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillMO skill, in SkillCastPolicy fallback)
        {
            if (skill == null) return fallback;

            return new SkillCastPolicy(
                fallback.AllowParallel || skill.SkillType == SkillType.ParallelActive,
                fallback.InterruptRunning || skill.InterruptRunning,
                skill.CastConflictGroup > 0 ? skill.CastConflictGroup : fallback.ConflictGroup,
                skill.InterruptPriority > 0 ? skill.InterruptPriority : fallback.InterruptPriority,
                fallback.Uninterruptible || skill.Uninterruptible,
                skill.TargetLostPolicy != 0 ? (SkillTargetLostPolicy)skill.TargetLostPolicy : fallback.TargetLostPolicy);
        }

        private MobaConfigDatabase ResolveConfigs()
        {
            if (_configResolved) return _configs;
            _configResolved = true;

            if (_services != null && _services.TryResolve<MobaConfigDatabase>(out var configs))
            {
                _configs = configs;
            }

            return _configs;
        }
    }
}
