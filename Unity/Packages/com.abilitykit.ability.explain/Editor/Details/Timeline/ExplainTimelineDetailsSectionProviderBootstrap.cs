using UnityEditor;

namespace AbilityKit.Ability.Explain.Editor
{
    internal static class ExplainTimelineDetailsSectionProviderBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            AbilityExplainRegistry.Register(new ExplainTimelineDetailsSectionProvider());
        }
    }
}
