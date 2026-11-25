using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudDamageEventPresenter
    {
        private readonly BattleHudFloatingTextController _floatingTexts;
        private readonly BattleHudDamageTextFormatter _formatter;

        public BattleHudDamageEventPresenter(
            BattleHudHpBarController hpBars,
            BattleHudFloatingTextController floatingTexts,
            BattleHudDamageTextFormatter formatter = null)
        {
            _floatingTexts = floatingTexts;
            _formatter = formatter ?? new BattleHudDamageTextFormatter();
        }

        public void Present(DamageEventData[] entries)
        {
            if (entries == null) return;

            for (var i = 0; i < entries.Length; i++)
            {
                Present(entries[i]);
            }
        }

        private void Present(in DamageEventData entry)
        {
            if (entry.TargetId <= 0) return;

            if (!_formatter.TryFormat(entry.Value, entry.IsHeal, out var text)) return;

            _floatingTexts.Spawn(entry.TargetId, text, entry.IsHeal);
        }
    }
}
