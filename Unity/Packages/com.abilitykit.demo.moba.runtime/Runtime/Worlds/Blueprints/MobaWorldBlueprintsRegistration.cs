using System;
using AbilityKit.Ability.Host.WorldBlueprints;

namespace AbilityKit.Ability.Share.Impl.Moba.Worlds.Blueprints
{
    public static class MobaWorldBlueprintsRegistration
    {
        public static void RegisterAll(WorldBlueprintRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            registry
                .Register(new MobaLobbyWorldBlueprint())
                .Register(new MobaBattleWorldBlueprint());
        }
    }
}
