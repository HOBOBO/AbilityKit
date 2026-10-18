using AbstractBattleDamageTextFormatter = AbilityKit.Demo.Moba.View.Abstractions.Battle.View.BattleDamageTextFormatter;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudDamageTextFormatter
    {
        private readonly AbstractBattleDamageTextFormatter _formatter;

        public BattleHudDamageTextFormatter(AbstractBattleDamageTextFormatter formatter = null)
        {
            _formatter = formatter ?? new AbstractBattleDamageTextFormatter();
        }

        public bool TryFormat(float value, bool isHeal, out string text)
        {
            return _formatter.TryFormat(value, isHeal, out text);
        }
    }
}
