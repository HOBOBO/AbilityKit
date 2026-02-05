using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleSessionNetAdapterContext
    {
        int InputDelayFrames { get; }

        IWorld RemoteDrivenWorld { get; }
        IWorld ConfirmedWorld { get; }

        IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenInputSource { get; set; }
        IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenConsumable { get; set; }
        IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> RemoteDrivenSink { get; set; }

        IRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedInputSource { get; set; }
        IConsumableRemoteFrameSource<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedConsumable { get; set; }
        IRemoteFrameSink<AbilityKit.Ability.Host.PlayerInputCommand[]> ConfirmedSink { get; set; }

        FrameSnapshotDispatcher Snapshots { get; }
    }

    internal sealed class BattleSessionNetAdapter
    {
        private readonly IBattleSessionNetAdapterContext _ctx;
        private readonly FramePacketNetAdapter _adapter;

        public BattleSessionNetAdapter(IBattleSessionNetAdapterContext ctx)
        {
            _ctx = ctx;
            _adapter = new FramePacketNetAdapter(new AdapterContext(ctx));
        }

        public FramePacket ProcessAndFeed(FramePacket packet)
        {
            packet = _adapter.ProcessAndFeed(packet);
            UpdateJitterBufferStats();
            return packet;
        }

        public FramePacket ProcessAndFeed(WorldId worldId, in RemoteInputFrame inputFrame, in RemoteSnapshotFrame snapshotFrame)
        {
            var packet = _adapter.ProcessAndFeed(worldId, inputFrame, snapshotFrame);
            UpdateJitterBufferStats();
            return packet;
        }

        private void UpdateJitterBufferStats()
        {
            if (_ctx.RemoteDrivenInputSource is AbilityKit.Network.Runtime.FrameJitterBuffer<PlayerInputCommand[]> jb)
            {
                AbilityKit.Game.Flow.BattleFlowDebugProvider.JitterBufferStats = new AbilityKit.Game.Flow.JitterBufferStatsSnapshot
                {
                    DelayFrames = jb.DelayFrames,
                    MissingMode = jb.MissingMode.ToString(),
                    TargetFrame = jb.TargetFrame,
                    MaxReceivedFrame = jb.MaxReceivedFrame,
                    LastConsumedFrame = jb.LastConsumedFrame,
                    BufferedCount = jb.Count,
                    MinBufferedFrame = jb.MinBufferedFrame,

                    AddedCount = jb.AddedCount,
                    DuplicateCount = jb.DuplicateCount,
                    LateCount = jb.LateCount,
                    ConsumedCount = jb.ConsumedCount,
                    FilledDefaultCount = jb.FilledDefaultCount,
                };
            }
        }

        private sealed class AdapterContext : IFramePacketNetAdapterContext
        {
            private readonly IBattleSessionNetAdapterContext _ctx;

            public AdapterContext(IBattleSessionNetAdapterContext ctx)
            {
                _ctx = ctx;
            }

            public int InputDelayFrames => _ctx.InputDelayFrames;

            public IWorld RemoteDrivenWorld => _ctx.RemoteDrivenWorld;
            public IWorld ConfirmedWorld => _ctx.ConfirmedWorld;

            public IRemoteFrameSource<PlayerInputCommand[]> RemoteDrivenInputSource
            {
                get => _ctx.RemoteDrivenInputSource;
                set => _ctx.RemoteDrivenInputSource = value;
            }

            public IConsumableRemoteFrameSource<PlayerInputCommand[]> RemoteDrivenConsumable
            {
                get => _ctx.RemoteDrivenConsumable;
                set => _ctx.RemoteDrivenConsumable = value;
            }

            public IRemoteFrameSink<PlayerInputCommand[]> RemoteDrivenSink
            {
                get => _ctx.RemoteDrivenSink;
                set => _ctx.RemoteDrivenSink = value;
            }

            public IRemoteFrameSource<PlayerInputCommand[]> ConfirmedInputSource
            {
                get => _ctx.ConfirmedInputSource;
                set => _ctx.ConfirmedInputSource = value;
            }

            public IConsumableRemoteFrameSource<PlayerInputCommand[]> ConfirmedConsumable
            {
                get => _ctx.ConfirmedConsumable;
                set => _ctx.ConfirmedConsumable = value;
            }

            public IRemoteFrameSink<PlayerInputCommand[]> ConfirmedSink
            {
                get => _ctx.ConfirmedSink;
                set => _ctx.ConfirmedSink = value;
            }

            public FrameSnapshotDispatcher Snapshots => _ctx.Snapshots;
        }
    }
}
