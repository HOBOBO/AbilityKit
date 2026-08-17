using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaTriggerPayloadResolver
    {
        int Priority { get; }
        bool TryCreateContext(
            in MobaCombatExecutionContext executionContext,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame,
            out MobaTriggerConditionContext context);
    }

    public sealed class MobaDefaultTriggerPayloadResolver : IMobaTriggerPayloadResolver
    {
        public int Priority => int.MinValue;

        public bool TryCreateContext(
            in MobaCombatExecutionContext executionContext,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame,
            out MobaTriggerConditionContext context)
        {
            context = MobaTriggerConditionContext.Create(
                in executionContext,
                skillRuntimes,
                frame);
            return true;
        }
    }

    [WorldService(typeof(MobaTriggerPayloadResolverRegistry))]
    public sealed class MobaTriggerPayloadResolverRegistry : IService
    {
        private readonly List<IMobaTriggerPayloadResolver> _resolvers = new List<IMobaTriggerPayloadResolver>();
        private bool _sorted;

        public MobaTriggerPayloadResolverRegistry()
        {
            Register(new MobaDefaultTriggerPayloadResolver());
        }

        public void Register(IMobaTriggerPayloadResolver resolver)
        {
            if (resolver == null) return;
            if (_resolvers.Contains(resolver)) return;
            _resolvers.Add(resolver);
            _sorted = false;
        }

        public bool TryCreateContext(
            in MobaCombatExecutionContext executionContext,
            MobaSkillCastRuntimeService skillRuntimes,
            int frame,
            out MobaTriggerConditionContext context)
        {
            EnsureSorted();

            for (int i = 0; i < _resolvers.Count; i++)
            {
                if (_resolvers[i].TryCreateContext(
                        in executionContext,
                        skillRuntimes,
                        frame,
                        out context))
                {
                    return true;
                }
            }

            context = default;
            return false;
        }

        private void EnsureSorted()
        {
            if (_sorted) return;
            _resolvers.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            _sorted = true;
        }

        public void Dispose()
        {
        }
    }
}
