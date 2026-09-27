using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;

namespace ET.Logic
{
    public static class ETBattleWorldSnapshotAdapter
    {
        public static bool TryConvert(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            switch (snapshot.OpCode)
            {
                case MobaOpCodes.Snapshot.ActorTransform:
                    return TryConvertActorTransform(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.ActorSpawn:
                    return TryConvertActorSpawn(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.ActorDespawn:
                    return TryConvertActorDespawn(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.SkillState:
                    return TryConvertSkillState(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.DamageEvent:
                    return TryConvertDamageEvent(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.ProjectileEvent:
                    return TryConvertProjectileEvent(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.AreaEvent:
                    return TryConvertAreaEvent(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.PresentationCue:
                    return TryConvertPresentationCue(in snapshot, frameIndex, timestamp, out frameSnapshot);

                case MobaOpCodes.Snapshot.StateHash:
                    return TryConvertStateHash(in snapshot, frameIndex, timestamp, out frameSnapshot);

                default:
                    frameSnapshot = default;
                    return false;
            }
        }

        private static bool TryConvertActorTransform(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!ActorTransformSnapshotRoute.TryDecode(in snapshot, out var transforms) || transforms.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                actorTransforms: transforms);
            return true;
        }

        private static bool TryConvertActorSpawn(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!ActorSpawnSnapshotRoute.TryDecode(in snapshot, out var spawns) || spawns.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                actorSpawns: spawns);
            return true;
        }

        private static bool TryConvertActorDespawn(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!ActorDespawnSnapshotRoute.TryDecode(in snapshot, out var despawns) || despawns.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                actorDespawns: despawns);
            return true;
        }

        private static bool TryConvertSkillState(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!SkillStateSnapshotRoute.TryDecode(in snapshot, out var states) || states.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                skillStates: states);
            return true;
        }

        private static bool TryConvertDamageEvent(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!DamageEventSnapshotRoute.TryDecode(in snapshot, out var entries) || entries.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                damageEvents: entries);
            return true;
        }

        private static bool TryConvertProjectileEvent(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!ProjectileEventSnapshotRoute.TryDecode(in snapshot, out var entries) || entries.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                projectileEvents: entries);
            return true;
        }

        private static bool TryConvertAreaEvent(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!AreaEventSnapshotRoute.TryDecode(in snapshot, out var entries) || entries.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                areaEvents: entries);
            return true;
        }

        private static bool TryConvertPresentationCue(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!PresentationCueSnapshotRoute.TryDecode(in snapshot, out var entries) || entries.Length == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                presentationCues: entries);
            return true;
        }

        private static bool TryConvertStateHash(in WorldStateSnapshot snapshot, int frameIndex, double timestamp, out FrameSnapshotData frameSnapshot)
        {
            if (!StateHashSnapshotRoute.TryDecode(in snapshot, out var stateHash)
                || stateHash.Version != StateHashSnapshotRoute.SupportedVersion
                || stateHash.FrameIndex < 0
                || stateHash.StateHash == 0)
            {
                frameSnapshot = default;
                return false;
            }

            frameSnapshot = new FrameSnapshotData(
                frameIndex: frameIndex,
                timestamp: timestamp,
                type: SnapshotType.Delta,
                stateHash: stateHash);
            return true;
        }
    }
}
