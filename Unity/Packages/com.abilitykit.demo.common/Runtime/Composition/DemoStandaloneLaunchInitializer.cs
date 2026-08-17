#nullable enable

using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using UnityEngine;

namespace AbilityKit.Demo.Common.Composition
{
    internal static class DemoStandaloneLaunchInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if !UNITY_EDITOR && ABILITYKIT_DEMO_MOBA_LOCAL
            RequestLocalLaunch(DemoGameplayId.Moba, "moba-local");
#elif !UNITY_EDITOR && ABILITYKIT_DEMO_SHOOTER_LOCAL
            RequestLocalLaunch(DemoGameplayId.Shooter, "shooter-local");
#endif
        }

#if !UNITY_EDITOR && (ABILITYKIT_DEMO_MOBA_LOCAL || ABILITYKIT_DEMO_SHOOTER_LOCAL)
        private static void RequestLocalLaunch(DemoGameplayId gameplay, string profileId)
        {
            DemoMultiplayerLaunchIntent.Clear();
            var request = new DemoLaunchRequest(gameplay, DemoLaunchMode.Local, profileId);
            DemoLaunchIntent.Request(in request);
        }
#endif
    }
}
