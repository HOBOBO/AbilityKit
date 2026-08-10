using AbilityKit.Network.Runtime.Conditioning;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Exposes gateway network conditioning while GatewaySessionRuntime owns its attachment lifecycle.
    /// </summary>
    public sealed partial class BattleSessionFeature
    {
        public NetworkConditionController NetworkCondition { get; } = new NetworkConditionController();
    }
}
