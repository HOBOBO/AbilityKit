using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
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
        private readonly BattleSessionDiagnostics _diagnostics;
        private readonly FramePacketNetAdapter _adapter;

        public BattleSessionNetAdapter(
            IBattleSessionNetAdapterContext ctx,
            BattleSessionDiagnostics diagnostics)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _adapter = new FramePacketNetAdapter(ctx);
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
                _diagnostics.PublishJitterBuffer(new JitterBufferStatsSnapshot
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
                });
            }
        }
    }
}
