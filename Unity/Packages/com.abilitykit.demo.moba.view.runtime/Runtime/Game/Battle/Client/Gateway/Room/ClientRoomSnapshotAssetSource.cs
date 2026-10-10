using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// 将 <see cref="ClientRoomSnapshot"/> 适配为 <see cref="IBattleAssetManifestSource"/>，
    /// 供 <see cref="BattleAssetManifestResolver"/> 使用。
    /// </summary>
    /// <remarks>
    /// 该适配器位于 View.Runtime（依赖 Shared.Assets），
    /// 使 Shared.Assets 无需反向引用上层快照类型，打破程序集循环依赖。
    /// </remarks>
    public sealed class ClientRoomSnapshotAssetSource : IBattleAssetManifestSource, IBattleGameplayManifestSource
    {
        private readonly ClientRoomSnapshot _snapshot;

        public ClientRoomSnapshotAssetSource(ClientRoomSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <inheritdoc />
        public IReadOnlyList<IBattleAssetManifestPlayer> Players
        {
            get
            {
                var raw = _snapshot.Players;
                if (raw == null || raw.Count == 0)
                {
                    return Array.Empty<IBattleAssetManifestPlayer>();
                }

                var adapted = new IBattleAssetManifestPlayer[raw.Count];
                for (var i = 0; i < raw.Count; i++)
                {
                    adapted[i] = new PlayerView(raw[i]);
                }
                return adapted;
            }
        }

        /// <inheritdoc />
        public int LaunchManifestVersion => _snapshot.LaunchManifestVersion;
        public int GameplayId => _snapshot.GameplayId;

        /// <inheritdoc />
        public string LaunchManifestHash => _snapshot.LaunchManifestHash;

        /// <inheritdoc />
        public long LaunchGeneration => _snapshot.LaunchGeneration;

        private sealed class PlayerView : IBattleAssetManifestPlayer
        {
            private readonly ClientRoomPlayer _player;

            public PlayerView(ClientRoomPlayer player)
            {
                _player = player;
            }

            public int HeroId => _player.HeroId;
            public int BasicAttackSkillId => _player.BasicAttackSkillId;
            public IReadOnlyList<int> SkillIds => _player.SkillIds ?? Array.Empty<int>();
        }
    }
}
