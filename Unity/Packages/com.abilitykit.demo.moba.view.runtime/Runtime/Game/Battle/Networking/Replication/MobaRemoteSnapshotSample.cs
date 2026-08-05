#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// A remote authoritative actor sample captured from a Moba gateway state-sync snapshot for
    /// <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> playback. Unlike the Shooter sample
    /// (which is 2D) Moba actors live in a 3D world, so the sample carries X/Y/Z plus a planar
    /// velocity (VelocityX/VelocityZ). The <see cref="TimelineTicks"/> used for buffering/interpolation
    /// is the snapshot frame promoted to ticks; Moba's <see cref="GatewayStateSyncSnapshot"/> does not
    /// yet carry authoritative server ticks, so the frame index acts as the monotonic timeline.
    /// </summary>
    public sealed class MobaRemoteSnapshotSample : IRemoteSnapshotSample
    {
        public MobaRemoteSnapshotSample(ulong worldId, int frame, IReadOnlyList<GatewayStateSyncActorSnapshot> actors)
        {
            WorldId = worldId;
            Frame = frame;
            Actors = actors ?? Array.Empty<GatewayStateSyncActorSnapshot>();
        }

        public ulong WorldId { get; }

        public int Frame { get; }

        public IReadOnlyList<GatewayStateSyncActorSnapshot> Actors { get; }

        public long TimelineTicks => Frame;
    }

    /// <summary>
    /// Builds an interpolated <see cref="GatewayStateSyncSnapshot"/> from a pair of bracketing
    /// <see cref="MobaRemoteSnapshotSample"/> samples so the Moba presentation pipeline can render
    /// delayed, smoothly interpolated remote actor state without any rollback. Mirrors the Shooter
    /// projector but blends the 3D position + planar velocity that Moba actors use.
    ///
    /// This is a stateful, single-consumer projector: it reuses its internal actor list and index
    /// across calls so steady-state playback does not allocate per frame.
    /// </summary>
    public sealed class MobaRemoteSnapshotProjector
    {
        private readonly List<GatewayStateSyncActorSnapshot> _actors = new List<GatewayStateSyncActorSnapshot>();
        private readonly Dictionary<int, GatewayStateSyncActorSnapshot> _fromById = new Dictionary<int, GatewayStateSyncActorSnapshot>();
        private readonly HashSet<int> _emitted = new HashSet<int>();
        private GatewayStateSyncActorSnapshot[] _cachedArray;

        /// <summary>
        /// Produces a snapshot whose actors are linearly interpolated between the two bracketing
        /// samples by <paramref name="interpolation"/>.Alpha. Actors present in both samples blend
        /// position/rotation/velocity/hp; actors present only in one side are handled so newly spawned
        /// remotes appear at their authoritative pose and despawning remotes hold their last pose
        /// through the in-between frame instead of popping out mid-interpolation.
        /// </summary>
        public GatewayStateSyncSnapshot Project(in RemoteSnapshotInterpolation<MobaRemoteSnapshotSample> interpolation)
        {
            var from = interpolation.From;
            var to = interpolation.To;
            float alpha = interpolation.Alpha;

            if (ReferenceEquals(from, to) || alpha <= 0f)
            {
                return BuildSnapshot(from, from.Actors);
            }

            if (alpha >= 1f)
            {
                return BuildSnapshot(to, to.Actors);
            }

            _actors.Clear();
            _emitted.Clear();
            BuildIndex(from.Actors);

            for (int i = 0; i < to.Actors.Count; i++)
            {
                var target = to.Actors[i];
                if (_fromById.TryGetValue(target.ActorId, out var source))
                {
                    _actors.Add(Lerp(in source, in target, alpha));
                }
                else
                {
                    _actors.Add(target);
                }

                _emitted.Add(target.ActorId);
            }

            for (int i = 0; i < from.Actors.Count; i++)
            {
                var source = from.Actors[i];
                if (!_emitted.Contains(source.ActorId))
                {
                    _actors.Add(source);
                }
            }

            return BuildSnapshot(to, _actors);
        }

        private GatewayStateSyncSnapshot BuildSnapshot(MobaRemoteSnapshotSample meta, IReadOnlyList<GatewayStateSyncActorSnapshot> actors)
        {
            var array = actors as GatewayStateSyncActorSnapshot[];
            if (array == null)
            {
                var count = actors.Count;
                if (_cachedArray == null || _cachedArray.Length < count)
                    _cachedArray = new GatewayStateSyncActorSnapshot[count];
                for (int i = 0; i < count; i++)
                    _cachedArray[i] = actors[i];
                array = _cachedArray;
            }

            return new GatewayStateSyncSnapshot(
                meta.WorldId,
                meta.Frame,
                0d,
                isFullSnapshot: true,
                array);
        }

        private void BuildIndex(IReadOnlyList<GatewayStateSyncActorSnapshot> actors)
        {
            _fromById.Clear();
            for (int i = 0; i < actors.Count; i++)
            {
                _fromById[actors[i].ActorId] = actors[i];
            }
        }

        private static GatewayStateSyncActorSnapshot Lerp(in GatewayStateSyncActorSnapshot from, in GatewayStateSyncActorSnapshot to, float alpha)
        {
            return new GatewayStateSyncActorSnapshot(
                to.ActorId,
                InterpolationMath.Lerp(from.X, to.X, alpha),
                InterpolationMath.Lerp(from.Y, to.Y, alpha),
                InterpolationMath.Lerp(from.Z, to.Z, alpha),
                // Rotation is an angle (radians). Blend along the shortest arc so values straddling the
                // ±π seam do not spin the long way around.
                InterpolationMath.LerpAngleRadians(from.Rotation, to.Rotation, alpha),
                InterpolationMath.Lerp(from.VelocityX, to.VelocityX, alpha),
                InterpolationMath.Lerp(from.VelocityZ, to.VelocityZ, alpha),
                InterpolationMath.Lerp(from.Hp, to.Hp, alpha),
                to.HpMax,
                to.TeamId,
                to.Kind,
                to.Code,
                to.OwnerNetId);
        }
    }
}
