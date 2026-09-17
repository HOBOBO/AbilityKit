using System;
using AbilityKit.BattleFlow;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
    public static class MobaPredictionBackendIds
    {
        public const string Headless = "headless";
        public const string UnityRoute = "unity-route";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Headless, StringComparison.OrdinalIgnoreCase)) return Headless;
            if (string.Equals(value, UnityRoute, StringComparison.OrdinalIgnoreCase)) return UnityRoute;
            throw new ArgumentException($"Unknown sync backend '{value}'.", nameof(value));
        }
    }

    public sealed class SetPredictionBackendBlock : MobaAssertionBlock
    {
        public string BackendId { get; set; } = MobaPredictionBackendIds.Headless;

        protected override void Apply(MobaBattleFlowAssertions assertions)
            => assertions.PredictionBackend = MobaPredictionBackendIds.Normalize(BackendId);
    }
}
