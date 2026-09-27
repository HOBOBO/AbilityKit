using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudDamageEventPresenter
    {
        private readonly BattleHudHpBarController _hpBars;
        private readonly BattleHudDamageTextFormatter _formatter;

        public BattleHudDamageEventPresenter(
            BattleHudHpBarController hpBars,
            BattleHudFloatingTextController floatingTexts,
            BattleHudDamageTextFormatter formatter = null)
        {
            _hpBars = hpBars;
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

            _hpBars.Ensure(entry.TargetId);
            _hpBars.UpdateHp(entry.TargetId, entry.TargetHp, entry.TargetMaxHp);
        }
    }
}
