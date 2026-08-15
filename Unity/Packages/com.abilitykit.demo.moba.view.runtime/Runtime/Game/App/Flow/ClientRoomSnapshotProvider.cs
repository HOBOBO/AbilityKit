#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 将权威 <see cref="ClientRoomStore"/> 投影为多人流程使用的稳定视图。
    /// </summary>
    public sealed class ClientRoomSnapshotProvider : IRoomSnapshotProvider, IDisposable
    {
        private readonly ClientRoomStore _store;

        public ClientRoomSnapshotProvider(ClientRoomStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _store.OnSnapshotChanged += HandleSnapshotChanged;
        }

        public MultiplayerRoomSnapshot? Current => Project(_store.Current);

        public event Action<MultiplayerRoomSnapshot>? OnSnapshotChanged;

        public void Dispose()
        {
            _store.OnSnapshotChanged -= HandleSnapshotChanged;
        }

        private void HandleSnapshotChanged(ClientRoomSnapshot snapshot)
        {
            var projected = Project(snapshot);
            if (projected != null)
            {
                OnSnapshotChanged?.Invoke(projected);
            }
        }

        private static MultiplayerRoomSnapshot? Project(ClientRoomSnapshot? snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new MultiplayerRoomSnapshot
            {
                RoomId = snapshot.RoomId,
                OwnerAccountId = snapshot.OwnerAccountId,
                NumericRoomId = snapshot.NumericRoomId,
                Phase = (MultiplayerRoomPhase)snapshot.Phase,
                PhaseReason = snapshot.PhaseReason,
                CanStart = snapshot.CanStart,
                BattleId = snapshot.BattleId,
                WorldId = snapshot.WorldId,
                LaunchGeneration = snapshot.LaunchGeneration,
                LaunchManifestVersion = snapshot.LaunchManifestVersion,
                LaunchManifestHash = snapshot.LaunchManifestHash,
                LoadingDeadlineUnixMs = snapshot.LoadingDeadlineUnixMs,
                RoomRevision = snapshot.RoomRevision,
                LastEventSequence = snapshot.LastEventSequence,
                LastStartFailureCode = snapshot.LastStartFailureCode,
                SyncCapabilities = snapshot.SyncCapabilities,
                Members = CopyStrings(snapshot.Members),
                Players = ToMultiplayerPlayers(snapshot.Players)
            };
        }

        private static IReadOnlyList<MultiplayerRoomPlayerSnapshot> ToMultiplayerPlayers(
            IReadOnlyList<ClientRoomPlayer> players)
        {
            if (players == null || players.Count == 0)
            {
                return Array.Empty<MultiplayerRoomPlayerSnapshot>();
            }

            var result = new MultiplayerRoomPlayerSnapshot[players.Count];
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                result[i] = new MultiplayerRoomPlayerSnapshot
                {
                    AccountId = player.AccountId,
                    PlayerId = player.PlayerId,
                    TeamId = player.TeamId,
                    HeroId = player.HeroId,
                    SpawnPointId = player.SpawnPointId,
                    Level = player.Level,
                    AttributeTemplateId = player.AttributeTemplateId,
                    BasicAttackSkillId = player.BasicAttackSkillId,
                    SkillIds = CopyInts(player.SkillIds),
                    LobbyReady = player.LobbyReady,
                    AssetsLoaded = player.AssetsLoaded,
                    LoadingProgress = player.LoadingProgress,
                    IsOnline = player.IsOnline,
                    JoinOrdinal = player.JoinOrdinal,
                    LoadedManifestVersion = player.LoadedManifestVersion,
                    LoadedManifestHash = player.LoadedManifestHash,
                    LastSeenTicks = player.LastSeenTicks,
                    OfflineSinceTicks = player.OfflineSinceTicks
                };
            }

            return result;
        }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
            var result = new string[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i] ?? string.Empty;
            return result;
        }

        private static IReadOnlyList<int> CopyInts(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<int>();
            var result = new int[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i];
            return result;
        }
    }
}
