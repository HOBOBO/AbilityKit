using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Game.Flow
{
    public partial class ConfirmedBattleViewFeature
    {
        protected override BattleContext RuntimeContext => _confirmedCtx;
        protected override bool RuntimeIsConfirmed => !_isPredictionView;
    }
}
