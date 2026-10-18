#nullable enable

using System;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// Minimal Moba-side adapter that reuses the framework <see cref="RemoteInterpolationPlayback{TSample}"/>
    /// for <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> style remote actor playback. It
    /// proves the framework extraction is genuinely demo-agnostic: Moba only supplies its own sample type
    /// (<see cref="MobaRemoteSnapshotSample"/>) and the "project to a Moba snapshot" half of the loop
    /// (<see cref="MobaRemoteSnapshotProjector"/>); the buffer + delayed timeline + extrapolation/starvation
    /// policy all live in the shared framework.
    ///
    /// Per-frame usage mirrors the Shooter controller:
    /// <code>
    /// playback.Observe(in incomingStateSyncSnapshot); // when a gateway state-sync push arrives
    /// playback.Advance(deltaSeconds);                 // every render frame
    /// if (playback.TryProjectRemoteFrame(out var snapshot)) { presentation.Apply(in snapshot); }
    /// </code>
    /// </summary>
    public sealed class MobaRemoteInterpolationPlayback
    {
        private readonly RemoteInterpolationPlayback<MobaRemoteSnapshotSample> _playback;
        private readonly MobaRemoteSnapshotProjector _projector = new MobaRemoteSnapshotProjector();

        public const int DefaultTickRate = 30;

        public MobaRemoteInterpolationPlayback()
            : this(DefaultTickRate)
        {
        }

        public MobaRemoteInterpolationPlayback(int tickRate)
            : this(CreateFrameTimelineConfig(tickRate))
        {
        }

        public MobaRemoteInterpolationPlayback(InterpolationConfig config)
        {
            _playback = new RemoteInterpolationPlayback<MobaRemoteSnapshotSample>(config);
        }

        /// <summary>Number of remote authoritative snapshots currently buffered for interpolation.</summary>
        public int BufferedRemoteSnapshotCount => _playback.BufferedSampleCount;

        /// <summary>The current delayed remote playback time, in timeline ticks (Moba frame index).</summary>
        public long RemotePlaybackTicks => _playback.PlaybackTicks;

        /// <summary>The current local estimate of authoritative server time, in timeline ticks.</summary>
        public long EstimatedServerTicks => _playback.EstimatedServerTicks;

        /// <summary>Whether at least one remote interpolation frame has been projected.</summary>
        public bool HasPublishedRemoteFrame => _playback.HasPublished;

        /// <summary>Whether the remote buffer is starved and playback is holding the last authoritative pose.</summary>
        public bool IsRemotePlaybackStarved => _playback.IsStarved;

        /// <summary>
        /// Buffers a remote authoritative gateway state-sync snapshot for delayed interpolation. This
        /// never imports state into the local simulation; it only feeds the framework buffer + timeline.
        /// </summary>
        /// <returns><c>true</c> when accepted, <c>false</c> when stale/duplicate.</returns>
        public bool Observe(in GatewayStateSyncSnapshot snapshot)
        {
            var sample = new MobaRemoteSnapshotSample(snapshot.WorldId, snapshot.Frame, snapshot.Actors);
            return _playback.Observe(sample);
        }

        /// <summary>
        /// Buffers an already-decoded remote authoritative sample for delayed interpolation. This is the
        /// strongly-typed seam used by the framework MobaClientAuthoritativeInterpolationSyncController
        /// when remotes arrive as <see cref="MobaRemoteSnapshotSample"/> rather than a raw gateway snapshot.
        /// </summary>
        /// <returns><c>true</c> when accepted, <c>false</c> when stale/duplicate.</returns>
        public bool Observe(in MobaRemoteSnapshotSample sample)
        {
            return _playback.Observe(sample);
        }

        /// <summary>Advances the delayed playback timeline by a frame delta.</summary>
        public void Advance(float deltaSeconds)
        {
            _playback.Advance(deltaSeconds);
        }

        /// <summary>
        /// Samples the framework playback at the current delayed time and projects the bracketing pair
        /// into an interpolated <see cref="GatewayStateSyncSnapshot"/>. Returns <c>false</c> until at
        /// least one authoritative time has been observed and the buffer is non-empty.
        /// </summary>
        public bool TryProjectRemoteFrame(out GatewayStateSyncSnapshot snapshot)
        {
            if (!_playback.TrySample(out var interpolation))
            {
                snapshot = default;
                return false;
            }

            snapshot = _projector.Project(in interpolation);
            return true;
        }

        /// <summary>Captures the current interpolation playback health for diagnostics / smoke output.</summary>
        public InterpolationDiagnostics GetInterpolationDiagnostics()
        {
            return _playback.GetDiagnostics();
        }

        internal static InterpolationConfig CreateFrameTimelineConfig(int tickRate)
        {
            var effectiveTickRate = tickRate > 0 ? tickRate : DefaultTickRate;
            var interpolationDelayFrames = Math.Max(1L, (long)Math.Ceiling(effectiveTickRate * 0.1d));
            var maxExtrapolationFrames = Math.Max(1L, (long)Math.Ceiling(effectiveTickRate * 0.05d));
            return new InterpolationConfig(
                ticksPerSecond: effectiveTickRate,
                interpolationDelayTicks: interpolationDelayFrames,
                bufferCapacity: 32,
                catchUpRate: InterpolationConfig.DefaultCatchUpRate,
                maxExtrapolationTicks: maxExtrapolationFrames);
        }

        /// <summary>Clears the buffer and resets the timeline and playback flags.</summary>
        public void Reset()
        {
            _playback.Reset();
        }
    }
}
