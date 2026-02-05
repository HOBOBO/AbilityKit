using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Ability.Host
{
    public interface IFramePacketNetAdapterContext
    {
        int InputDelayFrames { get; }

        IWorld RemoteDrivenWorld { get; }
        IWorld ConfirmedWorld { get; }

        IRemoteFrameSource<PlayerInputCommand[]> RemoteDrivenInputSource { get; set; }
        IConsumableRemoteFrameSource<PlayerInputCommand[]> RemoteDrivenConsumable { get; set; }
        IRemoteFrameSink<PlayerInputCommand[]> RemoteDrivenSink { get; set; }

        IRemoteFrameSource<PlayerInputCommand[]> ConfirmedInputSource { get; set; }
        IConsumableRemoteFrameSource<PlayerInputCommand[]> ConfirmedConsumable { get; set; }
        IRemoteFrameSink<PlayerInputCommand[]> ConfirmedSink { get; set; }

        FrameSnapshotDispatcher Snapshots { get; }
    }

    public sealed class FramePacketNetAdapter
    {
        private readonly IFramePacketNetAdapterContext _ctx;

        public FramePacketNetAdapter(IFramePacketNetAdapterContext ctx)
        {
            _ctx = ctx;
        }

        public FramePacket ProcessAndFeed(FramePacket packet)
        {
            packet = ProcessInput(packet);
            _ctx.Snapshots?.Feed(packet);
            return packet;
        }

        public FramePacket ProcessAndFeed(WorldId worldId, FrameIndex frame, PlayerInputCommand[] inputs, ISnapshotEnvelope[] envelopes)
        {
            var packet = new FramePacket(worldId, frame, inputs ?? Array.Empty<PlayerInputCommand>(), snapshot: default);
            packet = ProcessInput(packet);

            if (envelopes != null)
            {
                for (int i = 0; i < envelopes.Length; i++)
                {
                    _ctx.Snapshots?.Feed(envelopes[i]);
                }
            }

            return packet;
        }

        public FramePacket ProcessAndFeed(WorldId worldId, in RemoteInputFrame inputFrame, in RemoteSnapshotFrame snapshotFrame)
        {
            return ProcessAndFeed(worldId, inputFrame.Frame, inputFrame.Commands, snapshotFrame.Envelopes);
        }

        private FramePacket ProcessInput(FramePacket packet)
        {
            if (_ctx.RemoteDrivenWorld == null && _ctx.ConfirmedWorld == null) return packet;

            try
            {
                var frame = packet.Frame.Value;
                var worldId = _ctx.RemoteDrivenWorld != null ? _ctx.RemoteDrivenWorld.Id : packet.WorldId;

                var inputs = packet.Inputs == null || packet.Inputs.Count == 0
                    ? Array.Empty<PlayerInputCommand>()
                    : (packet.Inputs is PlayerInputCommand[] arr ? arr : new List<PlayerInputCommand>(packet.Inputs).ToArray());

                if (_ctx.RemoteDrivenInputSource == null)
                {
                    var delay = _ctx.InputDelayFrames < 0 ? 0 : _ctx.InputDelayFrames;
                    var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delay, MissingFrameMode.FillDefault, Array.Empty<PlayerInputCommand>);
                    _ctx.RemoteDrivenInputSource = buf;
                    _ctx.RemoteDrivenConsumable = buf;
                    _ctx.RemoteDrivenSink = buf;
                }

                _ctx.RemoteDrivenSink?.Add(frame, inputs);

                if (_ctx.ConfirmedInputSource == null)
                {
                    var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delayFrames: 0, missingMode: MissingFrameMode.FillDefault, missingFrameFactory: Array.Empty<PlayerInputCommand>, initialCapacity: 256);
                    _ctx.ConfirmedInputSource = buf;
                    _ctx.ConfirmedConsumable = buf;
                    _ctx.ConfirmedSink = buf;
                }

                _ctx.ConfirmedSink?.Add(frame, inputs);

                return new FramePacket(worldId, new FrameIndex(frame), packet.Inputs, default);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                return packet;
            }
        }
    }
}
