using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSnapshotRouter : IWorldStateSnapshotProvider
    {
        private readonly MobaEnterGameSnapshotService _enter;
        private readonly MobaActorSpawnSnapshotService _spawn;
        private readonly MobaProjectileEventSnapshotService _projectileEvents;
        private readonly MobaActorTransformSnapshotService _transform;
        private readonly MobaLobbySnapshotService _lobby;
        private readonly MobaStateHashSnapshotService _hash;

        public MobaSnapshotRouter(MobaEnterGameSnapshotService enter, MobaActorSpawnSnapshotService spawn, MobaProjectileEventSnapshotService projectileEvents, MobaActorTransformSnapshotService transform, MobaLobbySnapshotService lobby, MobaStateHashSnapshotService hash)
        {
            _enter = enter ?? throw new ArgumentNullException(nameof(enter));
            _spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            _projectileEvents = projectileEvents ?? throw new ArgumentNullException(nameof(projectileEvents));
            _transform = transform ?? throw new ArgumentNullException(nameof(transform));
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_enter.TryGetSnapshot(frame, out snapshot)) return true;
            if (_spawn.TryGetSnapshot(frame, out snapshot)) return true;
            if (_projectileEvents.TryGetSnapshot(frame, out snapshot)) return true;
            if (_hash.TryGetSnapshot(frame, out snapshot)) return true;
            if (_transform.TryGetSnapshot(frame, out snapshot)) return true;
            return _lobby.TryGetSnapshot(frame, out snapshot);
        }

        public void Dispose()
        {
        }
    }

    public sealed class MobaProjectileEventSnapshotService : IService
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly IProjectileService _projectiles;
        private readonly MobaProjectileLinkService _links;

        private FrameIndex _lastFrame;

        private readonly List<ProjectileSpawnEvent> _spawns = new List<ProjectileSpawnEvent>(32);
        private readonly List<ProjectileHitEvent> _hits = new List<ProjectileHitEvent>(32);
        private readonly List<ProjectileExitEvent> _exits = new List<ProjectileExitEvent>(32);

        public MobaProjectileEventSnapshotService(MobaLobbyStateService lobby, IProjectileService projectiles, MobaProjectileLinkService links)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
            _links = links;
            _lastFrame = new FrameIndex(-999999);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_lobby.Started)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }
            _lastFrame = frame;

            _spawns.Clear();
            _hits.Clear();
            _exits.Clear();

            _projectiles.DrainSpawnEvents(_spawns);
            _projectiles.DrainHitEvents(_hits);
            _projectiles.DrainExitEvents(_exits);

            if (_spawns.Count == 0 && _hits.Count == 0 && _exits.Count == 0)
            {
                snapshot = default;
                return false;
            }

            var entries = new List<MobaProjectileEventSnapshotCodec.Entry>(_spawns.Count + _hits.Count + _exits.Count);

            for (int i = 0; i < _spawns.Count; i++)
            {
                var e = _spawns[i];
                var it = MobaProjectileEventSnapshotCodec.Entry.FromSpawn(in e);
                if (_links != null && _links.TryGetActorId(e.Projectile, out var projectileActorId) && projectileActorId > 0)
                {
                    it = new MobaProjectileEventSnapshotCodec.Entry(
                        kind: it.Kind,
                        projectileActorId: projectileActorId,
                        ownerActorId: it.OwnerActorId,
                        templateId: it.TemplateId,
                        launcherActorId: it.LauncherActorId,
                        rootActorId: it.RootActorId,
                        x: it.X,
                        y: it.Y,
                        z: it.Z,
                        hitCollider: it.HitCollider,
                        exitReason: it.ExitReason);
                }
                entries.Add(it);
            }

            for (int i = 0; i < _hits.Count; i++)
            {
                var e = _hits[i];
                var it = MobaProjectileEventSnapshotCodec.Entry.FromHit(in e);
                if (_links != null && _links.TryGetActorId(e.Projectile, out var projectileActorId) && projectileActorId > 0)
                {
                    it = new MobaProjectileEventSnapshotCodec.Entry(
                        kind: it.Kind,
                        projectileActorId: projectileActorId,
                        ownerActorId: it.OwnerActorId,
                        templateId: it.TemplateId,
                        launcherActorId: it.LauncherActorId,
                        rootActorId: it.RootActorId,
                        x: it.X,
                        y: it.Y,
                        z: it.Z,
                        hitCollider: it.HitCollider,
                        exitReason: it.ExitReason);
                }
                entries.Add(it);
            }

            for (int i = 0; i < _exits.Count; i++)
            {
                var e = _exits[i];
                var it = MobaProjectileEventSnapshotCodec.Entry.FromExit(in e);
                if (_links != null && _links.TryGetActorId(e.Projectile, out var projectileActorId) && projectileActorId > 0)
                {
                    it = new MobaProjectileEventSnapshotCodec.Entry(
                        kind: it.Kind,
                        projectileActorId: projectileActorId,
                        ownerActorId: it.OwnerActorId,
                        templateId: it.TemplateId,
                        launcherActorId: it.LauncherActorId,
                        rootActorId: it.RootActorId,
                        x: it.X,
                        y: it.Y,
                        z: it.Z,
                        hitCollider: it.HitCollider,
                        exitReason: it.ExitReason);
                }
                entries.Add(it);
            }

            var payload = MobaProjectileEventSnapshotCodec.Serialize(entries.ToArray());
            snapshot = new WorldStateSnapshot((int)MobaOpCode.ProjectileEventSnapshot, payload);
            return true;
        }

        public void Dispose()
        {
        }
    }

    public static class MobaProjectileEventSnapshotCodec
    {
        public enum EventKind
        {
            Spawn = 1,
            Hit = 2,
            Exit = 3,
        }

        public static byte[] Serialize(Entry[] entries)
        {
            entries ??= Array.Empty<Entry>();
            return BinaryObjectCodec.Encode(new SnapshotPayload(entries));
        }

        public static Entry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length < 4) return Array.Empty<Entry>();
            var p = BinaryObjectCodec.Decode<SnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<Entry>();
        }

        public readonly struct SnapshotPayload
        {
            [BinaryMember(0)] public readonly Entry[] Entries;

            public SnapshotPayload(Entry[] entries)
            {
                Entries = entries;
            }
        }

        public readonly struct Entry
        {
            [BinaryMember(0)] public readonly int Kind;
            [BinaryMember(1)] public readonly int ProjectileActorId;
            [BinaryMember(2)] public readonly int OwnerActorId;
            [BinaryMember(3)] public readonly int TemplateId;
            [BinaryMember(4)] public readonly int LauncherActorId;
            [BinaryMember(5)] public readonly int RootActorId;
            [BinaryMember(6)] public readonly float X;
            [BinaryMember(7)] public readonly float Y;
            [BinaryMember(8)] public readonly float Z;
            [BinaryMember(9)] public readonly int HitCollider;
            [BinaryMember(10)] public readonly int ExitReason;

            public Entry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason)
            {
                Kind = kind;
                ProjectileActorId = projectileActorId;
                OwnerActorId = ownerActorId;
                TemplateId = templateId;
                LauncherActorId = launcherActorId;
                RootActorId = rootActorId;
                X = x;
                Y = y;
                Z = z;
                HitCollider = hitCollider;
                ExitReason = exitReason;
            }

            public static Entry FromSpawn(in ProjectileSpawnEvent e)
            {
                return new Entry(
                    kind: (int)EventKind.Spawn,
                    projectileActorId: 0,
                    ownerActorId: e.OwnerId,
                    templateId: e.TemplateId,
                    launcherActorId: e.LauncherActorId,
                    rootActorId: e.RootActorId,
                    x: e.Position.X,
                    y: e.Position.Y,
                    z: e.Position.Z,
                    hitCollider: 0,
                    exitReason: 0);
            }

            public static Entry FromHit(in ProjectileHitEvent e)
            {
                return new Entry(
                    kind: (int)EventKind.Hit,
                    projectileActorId: 0,
                    ownerActorId: e.OwnerId,
                    templateId: e.TemplateId,
                    launcherActorId: e.LauncherActorId,
                    rootActorId: e.RootActorId,
                    x: e.Point.X,
                    y: e.Point.Y,
                    z: e.Point.Z,
                    hitCollider: e.HitCollider.Value,
                    exitReason: 0);
            }

            public static Entry FromExit(in ProjectileExitEvent e)
            {
                return new Entry(
                    kind: (int)EventKind.Exit,
                    projectileActorId: 0,
                    ownerActorId: e.OwnerId,
                    templateId: e.TemplateId,
                    launcherActorId: e.LauncherActorId,
                    rootActorId: e.RootActorId,
                    x: e.Position.X,
                    y: e.Position.Y,
                    z: e.Position.Z,
                    hitCollider: 0,
                    exitReason: (int)e.Reason);
            }
        }
    }
}
