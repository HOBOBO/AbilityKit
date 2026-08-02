#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Shooter.View
{
    public enum ShooterRoomSessionState
    {
        Idle = 0,
        CreatingRoom,
        JoiningRoom,
        InLobby,
        LoadingAssets,
        WaitingForBattle,
        InBattle,
        Failed,
        LeavingRoom
    }

    public enum ShooterRoomSessionPhase
    {
        Lobby = 0,
        Loading = 1,
        Starting = 2,
        InBattle = 3,
        Closing = 4,
        Closed = 5,
        Expired = 6
    }

    public sealed class ShooterRoomSessionMember
    {
        public ShooterRoomSessionMember(
            string accountId,
            uint playerId,
            bool isOnline,
            bool lobbyReady,
            bool assetsLoaded,
            int loadingProgress)
        {
            AccountId = accountId ?? string.Empty;
            PlayerId = playerId;
            IsOnline = isOnline;
            LobbyReady = lobbyReady;
            AssetsLoaded = assetsLoaded;
            LoadingProgress = Math.Max(0, Math.Min(100, loadingProgress));
        }

        public string AccountId { get; }
        public uint PlayerId { get; }
        public bool IsOnline { get; }
        public bool LobbyReady { get; }
        public bool AssetsLoaded { get; }
        public int LoadingProgress { get; }
    }

    public sealed class ShooterRoomSessionSnapshot
    {
        public ShooterRoomSessionSnapshot(
            string roomId,
            string ownerAccountId,
            ShooterRoomSessionPhase phase,
            string phaseReason,
            long launchGeneration,
            string launchManifestHash,
            int launchManifestVersion,
            long roomRevision,
            bool canStart,
            string battleId,
            ulong worldId,
            in ShooterGatewayWorldStartAnchor worldStartAnchor,
            IReadOnlyList<ShooterRoomSessionMember>? members = null)
        {
            RoomId = roomId ?? string.Empty;
            OwnerAccountId = ownerAccountId ?? string.Empty;
            Phase = phase;
            PhaseReason = phaseReason ?? string.Empty;
            LaunchGeneration = launchGeneration;
            LaunchManifestHash = launchManifestHash ?? string.Empty;
            LaunchManifestVersion = launchManifestVersion;
            RoomRevision = roomRevision;
            CanStart = canStart;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            WorldStartAnchor = worldStartAnchor;
            Members = members ?? Array.Empty<ShooterRoomSessionMember>();
        }

        public string RoomId { get; }
        public string OwnerAccountId { get; }
        public ShooterRoomSessionPhase Phase { get; }
        public string PhaseReason { get; }
        public long LaunchGeneration { get; }
        public string LaunchManifestHash { get; }
        public int LaunchManifestVersion { get; }
        public long RoomRevision { get; }
        public bool CanStart { get; }
        public string BattleId { get; }
        public ulong WorldId { get; }
        public ShooterGatewayWorldStartAnchor WorldStartAnchor { get; }
        public IReadOnlyList<ShooterRoomSessionMember> Members { get; }

        public bool IsOwner(uint playerId)
        {
            if (playerId == 0u || string.IsNullOrWhiteSpace(OwnerAccountId)) return false;
            for (var i = 0; i < Members.Count; i++)
            {
                var member = Members[i];
                if (member.PlayerId == playerId)
                {
                    return string.Equals(member.AccountId, OwnerAccountId, StringComparison.Ordinal);
                }
            }

            return false;
        }

        public ShooterRoomSessionMember? FindMember(uint playerId)
        {
            for (var i = 0; i < Members.Count; i++)
            {
                if (Members[i].PlayerId == playerId) return Members[i];
            }

            return null;
        }

        internal static ShooterRoomSessionSnapshot FromGateway(ShooterGatewayStagedRoomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var source = snapshot.Players;
            var members = new ShooterRoomSessionMember[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var player = source[i];
                members[i] = new ShooterRoomSessionMember(
                    player.AccountId,
                    player.PlayerId,
                    player.IsOnline,
                    player.LobbyReady,
                    player.AssetsLoaded,
                    player.LoadingProgress);
            }

            var anchor = snapshot.WorldStartAnchor;
            return new ShooterRoomSessionSnapshot(
                snapshot.RoomId,
                snapshot.OwnerAccountId,
                (ShooterRoomSessionPhase)snapshot.Phase,
                snapshot.PhaseReason,
                snapshot.LaunchGeneration,
                snapshot.LaunchManifestHash,
                snapshot.LaunchManifestVersion,
                snapshot.RoomRevision,
                snapshot.CanStart,
                snapshot.BattleId,
                snapshot.WorldId,
                in anchor,
                members);
        }
    }

    public readonly struct ShooterRoomSessionLaunchSpec
    {
        public ShooterRoomSessionLaunchSpec(
            string sessionToken,
            in ShooterRoomLaunchSpec roomLaunchSpec,
            uint fallbackPlayerId,
            TimeSpan? timeout = null)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomLaunchSpec = roomLaunchSpec;
            FallbackPlayerId = fallbackPlayerId;
            Timeout = timeout;
        }

        public string SessionToken { get; }
        public ShooterRoomLaunchSpec RoomLaunchSpec { get; }
        public uint FallbackPlayerId { get; }
        public TimeSpan? Timeout { get; }
    }

    public readonly struct ShooterRoomSessionJoinResult
    {
        public ShooterRoomSessionJoinResult(
            string roomId,
            ulong numericRoomId,
            uint playerId,
            ShooterRoomGatewayEntryKind entryKind,
            string battleId,
            string message)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            PlayerId = playerId;
            EntryKind = entryKind;
            BattleId = battleId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string RoomId { get; }
        public ulong NumericRoomId { get; }
        public uint PlayerId { get; }
        public ShooterRoomGatewayEntryKind EntryKind { get; }
        public string BattleId { get; }
        public string Message { get; }
        public bool JoinedRunningBattle => EntryKind != ShooterRoomGatewayEntryKind.TeamLobby &&
                                           !string.IsNullOrWhiteSpace(BattleId);
    }
}
