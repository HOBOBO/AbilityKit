namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void ResetSessionHandles() => _handles.ResetSessionResources();

        private void ResetHandles() => _handles.Reset();
    }
}
