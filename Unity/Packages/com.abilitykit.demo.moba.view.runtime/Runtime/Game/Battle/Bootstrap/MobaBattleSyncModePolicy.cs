using System;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Centralizes the user-facing capability boundary for MOBA sync modes.
    /// The plan and runtime still own their transport details; this policy only
    /// decides which combinations may be selected and gives the UI a stable
    /// explanation when a profile is not available yet.
    /// </summary>
    public static class MobaBattleSyncModePolicy
    {
        public static bool TryValidateCombination(
            BattleHostMode hostMode,
            BattleSyncMode syncMode,
            bool useGatewayTransport,
            out string reason)
        {
            reason = string.Empty;

            if (!Enum.IsDefined(typeof(BattleHostMode), hostMode))
            {
                reason = $"Unsupported host mode: {hostMode}.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BattleSyncMode), syncMode))
            {
                reason = $"Unsupported sync mode: {syncMode}.";
                return false;
            }

            if (hostMode == BattleHostMode.GatewayRemote && !useGatewayTransport)
            {
                reason = "GatewayRemote requires the configured gateway transport.";
                return false;
            }

            if (hostMode == BattleHostMode.GatewayRemote && syncMode == BattleSyncMode.HybridPredictReconcile)
            {
                reason = "GatewayRemote HybridPredictReconcile is not declared by the MOBA room sync catalog.";
                return false;
            }

            return true;
        }

        public static string GetGatewayProfileName(BattleSyncMode syncMode)
        {
            return syncMode switch
            {
                BattleSyncMode.Lockstep => "Lockstep",
                BattleSyncMode.SnapshotAuthority => "Moba.AuthoritativeRemoteInterpolation",
                _ => throw new ArgumentOutOfRangeException(nameof(syncMode), syncMode, "Unsupported Gateway MOBA sync mode.")
            };
        }

        /// <summary>
        /// Returns whether the profile can be launched by the local demo lobby.
        /// StateSync remains a valid plan-level mode, but its local authority
        /// transport is deliberately not advertised until the producer/import
        /// path is complete.
        /// </summary>
        public static bool CanSelectInLocalDemo(BattleStartPresetSO preset, out string reason)
        {
            reason = string.Empty;
            if (preset == null)
            {
                reason = "The sync profile is missing.";
                return false;
            }

            if (!TryValidateCombination(
                    preset.HostMode,
                    preset.SyncMode,
                    preset.GatewaySO != null && preset.GatewaySO.UseGatewayTransport,
                    out reason))
            {
                return false;
            }

            if (preset.HostMode != BattleHostMode.Local)
            {
                reason = "GatewayRemote is entered through the formal multiplayer lobby.";
                return false;
            }

            if (preset.SyncMode != BattleSyncMode.Lockstep)
            {
                reason = "Local state-sync authority is not selectable until its snapshot transport is complete.";
                return false;
            }

            return true;
        }

        public static bool CanSelectInSyncModeLobby(BattleStartPresetSO preset, out string reason)
        {
            if (preset != null && preset.HostMode == BattleHostMode.GatewayRemote)
            {
                return TryValidateCombination(
                    preset.HostMode,
                    preset.SyncMode,
                    preset.GatewaySO != null && preset.GatewaySO.UseGatewayTransport,
                    out reason);
            }

            return CanSelectInLocalDemo(preset, out reason);
        }

        public static string GetDisplayName(BattleSyncMode syncMode)
        {
            switch (syncMode)
            {
                case BattleSyncMode.Lockstep:
                    return "MOBA 帧同步";
                case BattleSyncMode.SnapshotAuthority:
                    return "MOBA 状态同步";
                case BattleSyncMode.HybridPredictReconcile:
                    return "MOBA 预测校正";
                default:
                    return syncMode.ToString();
            }
        }

        public static string GetProfileDescription(BattleSyncMode syncMode)
        {
            switch (syncMode)
            {
                case BattleSyncMode.Lockstep:
                    return "按确认帧推进共享 MOBA 逻辑。";
                case BattleSyncMode.SnapshotAuthority:
                    return "GatewayRemote uses authoritative snapshots; local authority transport is still pending.";
                case BattleSyncMode.HybridPredictReconcile:
                    return "本地预测并按权威状态校正，使用专用验证预设。";
                default:
                    return "未定义同步模式。";
            }
        }
    }
}
