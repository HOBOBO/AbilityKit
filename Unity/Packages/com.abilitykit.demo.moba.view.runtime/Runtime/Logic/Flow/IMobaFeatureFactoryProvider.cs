using System;

namespace AbilityKit.Game.Flow
{
    public interface IMobaFeatureFactoryProvider
    {
        MobaFeatureFactoryRegistry CreateFeatureFactoryRegistry(Func<IBattleSessionFeature> createBattleSessionFeature);
    }
}
