using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

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

        public void Present(MobaDamageEventSnapshotEntry[] entries)
        {
            if (entries == null) return;

            for (var i = 0; i < entries.Length; i++)
            {
                Present(entries[i]);
            }
        }

        private void Present(in MobaDamageEventSnapshotEntry entry)
        {
            if (entry.TargetActorId <= 0) return;

            var isHeal = entry.Kind == (int)DamageEventKind.Heal;
            if (!_formatter.TryFormat(entry.Value, isHeal, out var text)) return;

            _hpBars.Ensure(entry.TargetActorId);
            _hpBars.UpdateHp(entry.TargetActorId, entry.TargetHp, entry.TargetMaxHp);
        }
    }
}
