#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Room
{
    public sealed class RoomGatewaySnapshot
    {
        public string RoomId { get; set; } = string.Empty;
        public string OwnerAccountId { get; set; } = string.Empty;
        public RoomGatewaySessionPhase Phase { get; set; }
        public string PhaseReason { get; set; } = string.Empty;
        public long LaunchGeneration { get; set; }
        public long LoadingDeadlineUnixMs { get; set; }
        public string LaunchManifestHash { get; set; } = string.Empty;
        public int LaunchManifestVersion { get; set; }
        public string LastStartFailureCode { get; set; } = string.Empty;
        public long RoomRevision { get; set; }
        public long LastEventSequence { get; set; }
        public bool CanStart { get; set; }
        public string BattleId { get; set; } = string.Empty;
        public ulong WorldId { get; set; }
        public IReadOnlyList<string> Members { get; set; } = Array.Empty<string>();
        public IReadOnlyList<RoomGatewayPlayerSnapshot> Players { get; set; } = Array.Empty<RoomGatewayPlayerSnapshot>();
        public RoomGatewayWorldStartAnchor WorldStartAnchor { get; set; }
    }

    public sealed class RoomGatewayPlayerSnapshot
    {
        public string AccountId { get; set; } = string.Empty;
        public uint PlayerId { get; set; }
        public int TeamId { get; set; }
        public bool Ready { get; set; }
        public int HeroId { get; set; }
        public int SpawnPointId { get; set; }
        public int Level { get; set; }
        public int AttributeTemplateId { get; set; }
        public int BasicAttackSkillId { get; set; }
        public IReadOnlyList<int> SkillIds { get; set; } = Array.Empty<int>();
        public bool LobbyReady { get; set; }
        public bool AssetsLoaded { get; set; }
        public int LoadingProgress { get; set; }
        public bool IsOnline { get; set; }
        public long JoinOrdinal { get; set; }
        public int LoadedManifestVersion { get; set; }
        public string LoadedManifestHash { get; set; } = string.Empty;
        public long LastSeenTicks { get; set; }
        public long OfflineSinceTicks { get; set; }
    }

    /// <summary>
    /// 阶段化恢复结果。
    /// </summary>
    public readonly struct RoomGatewayStagedRestoreResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly RoomGatewaySnapshot? Snapshot;
        public readonly RoomGatewaySessionPhase Phase;
        public readonly RoomGatewayStagedRestoreNextStep NextStep;
        public readonly uint PlayerId;
        public readonly long ServerNowTicks;
        public readonly string Message;
        public readonly RoomGatewaySessionEntryKind EntryKind;
        public readonly bool CanStart;
        public readonly RoomGatewaySessionRestoreStatus RestoreStatus;
        public readonly RoomGatewaySessionRestoreErrorCode RestoreErrorCode;

        public bool CanRetry =>
            RestoreStatus == RoomGatewaySessionRestoreStatus.Timeout ||
            (RestoreStatus == RoomGatewaySessionRestoreStatus.Failed &&
             RestoreErrorCode == RoomGatewaySessionRestoreErrorCode.InternalError);

        public RoomGatewayStagedRestoreResult(
            string roomId,
            ulong numericRoomId,
            RoomGatewaySnapshot? snapshot,
            RoomGatewaySessionPhase phase,
            RoomGatewayStagedRestoreNextStep nextStep,
            uint playerId,
            long serverNowTicks,
            string message,
            RoomGatewaySessionEntryKind entryKind,
            bool canStart,
            RoomGatewaySessionRestoreStatus restoreStatus,
            RoomGatewaySessionRestoreErrorCode restoreErrorCode)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Snapshot = snapshot;
            Phase = phase;
            NextStep = nextStep;
            PlayerId = playerId;
            ServerNowTicks = serverNowTicks;
            Message = message ?? string.Empty;
            EntryKind = entryKind;
            CanStart = canStart;
            RestoreStatus = restoreStatus;
            RestoreErrorCode = restoreErrorCode;
        }
    }
}
