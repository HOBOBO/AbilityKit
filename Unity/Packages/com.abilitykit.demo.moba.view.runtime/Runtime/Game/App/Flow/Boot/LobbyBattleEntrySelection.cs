using System;

namespace AbilityKit.Game.Flow
{
    public sealed class LobbyBattleEntrySelection
    {
        public event Action Changed;

        public BattleStartConfig Config { get; private set; }
        public BattleStartPresetSO Preset { get; private set; }

        public bool IsRemoteSelected =>
            Preset != null &&
            Preset.HostMode == BattleHostMode.GatewayRemote;

        public void SelectRemote(BattleStartConfig config, BattleStartPresetSO preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (preset.HostMode != BattleHostMode.GatewayRemote)
            {
                throw new ArgumentException("Only a GatewayRemote preset can activate the multiplayer lobby.", nameof(preset));
            }

            Config = config;
            Preset = preset;
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (Config == null && Preset == null) return;

            Config = null;
            Preset = null;
            Changed?.Invoke();
        }
    }
}
