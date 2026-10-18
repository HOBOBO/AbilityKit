using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Battle
{
    internal interface IRemoteFrameStreams : IDisposable
    {
        IRemoteFrameSource<RemoteInputFrame> InputFrames { get; }
        IRemoteFrameSink<RemoteInputFrame> InputSink { get; }
        IRemoteFrameSource<RemoteSnapshotFrame> SnapshotFrames { get; }
        IRemoteFrameSink<RemoteSnapshotFrame> SnapshotSink { get; }
        void OnFrameReceived(FramePacket packet);
    }

    internal static class RemoteFrameStreamsFactory
    {
        public static IRemoteFrameStreams Create() => new RemoteFrameStreamHub();
    }

    internal sealed class RemoteFrameStreamHub : IRemoteFrameStreams
    {
        private const int RetainedFrames = 256;

        private readonly RemoteFrameAggregator _aggregator = new RemoteFrameAggregator();
        private RemoteFrameBuffer<RemoteInputFrame> _inputFrames;
        private RemoteFrameBuffer<RemoteSnapshotFrame> _snapshotFrames;

        public IRemoteFrameSource<RemoteInputFrame> InputFrames
        {
            get
            {
                EnsureCreated();
                return _inputFrames;
            }
        }

        public IRemoteFrameSink<RemoteInputFrame> InputSink
        {
            get
            {
                EnsureCreated();
                return _inputFrames;
            }
        }

        public IRemoteFrameSource<RemoteSnapshotFrame> SnapshotFrames
        {
            get
            {
                EnsureCreated();
                return _snapshotFrames;
            }
        }

        public IRemoteFrameSink<RemoteSnapshotFrame> SnapshotSink
        {
            get
            {
                EnsureCreated();
                return _snapshotFrames;
            }
        }

        public void OnFrameReceived(FramePacket packet)
        {
            if (packet == null) return;
            EnsureCreated();

            var frame = packet.Frame.Value;
            _aggregator.AddPacket(packet);
            _inputFrames.Add(frame, _aggregator.BuildInputFrame(packet.Frame));
            _snapshotFrames.Add(frame, _aggregator.BuildSnapshotFrame(packet.Frame));

            var trimBefore = frame - RetainedFrames;
            if (trimBefore <= 0) return;

            _aggregator.TrimBefore(trimBefore);
            _inputFrames.TrimBefore(trimBefore);
            _snapshotFrames.TrimBefore(trimBefore);
        }

        public void Dispose()
        {
            _inputFrames?.Dispose();
            _snapshotFrames?.Dispose();
            _inputFrames = null;
            _snapshotFrames = null;
        }

        private void EnsureCreated()
        {
            _inputFrames ??= new RemoteFrameBuffer<RemoteInputFrame>(initialCapacity: RetainedFrames);
            _snapshotFrames ??= new RemoteFrameBuffer<RemoteSnapshotFrame>(initialCapacity: RetainedFrames);
        }
    }
}
