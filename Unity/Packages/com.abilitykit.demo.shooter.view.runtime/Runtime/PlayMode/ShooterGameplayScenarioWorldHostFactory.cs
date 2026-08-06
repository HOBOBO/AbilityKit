#nullable enable

using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Shooter.Runtime;
#if UNITY_5_3_OR_NEWER
using AbilityKit.Demo.Shooter.Jobs;
#endif

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    public static class ShooterGameplayScenarioWorldHostFactory
    {
        public static ShooterBattleWorldSession CreateBattleWorld(
            string? worldId,
            ShooterPlayModeSessionOptions sessionOptions)
        {
            return ShooterBattleWorldSession.Create(
                worldId,
                Create(sessionOptions.GameplayScenario));
        }

        public static ShooterWorldHost Create(ShooterSveltoGameplayScenarioConfig? scenario)
        {
            return new ShooterWorldHost(options => ConfigureWorldOptions(options, scenario));
        }

        public static void ConfigureWorldOptions(
            WorldCreateOptions options,
            ShooterSveltoGameplayScenarioConfig? scenario)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

#if UNITY_5_3_OR_NEWER
            options.Modules.Add(new ShooterUnityJobsWorldModule());
#endif
            if (scenario.HasValue)
            {
                options.Extensions[typeof(ShooterSveltoGameplayScenarioConfig)] = scenario.Value;
            }
        }

        public static void ConfigureWorldOptions(
            WorldCreateOptions options,
            in ShooterSveltoGameplayScenarioConfig scenario)
        {
            ConfigureWorldOptions(options, (ShooterSveltoGameplayScenarioConfig?)scenario);
        }
    }
}
