using AbilityKit.Network.Runtime.Conditioning;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Exposes room-preparation and battle connection conditioning for this session.
    /// </summary>
    public sealed partial class BattleSessionFeature
    {
        public NetworkConditionController NetworkCondition { get; } = new NetworkConditionController();
    }
}
