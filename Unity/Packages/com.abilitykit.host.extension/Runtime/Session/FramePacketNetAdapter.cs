using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Ability.Host.Extensions.Session
{
    /// <summary>
    /// Context interface for FramePacketNetAdapter.
    /// Provides access to frame buffers, snapshot dispatcher, and world instances.
    /// </summary>
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

        AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher Snapshots { get; }
    }

    /// <summary>
    /// Processes incoming frame packets: routes inputs to frame buffers
    /// and feeds snapshots to the dispatcher.
    ///
    /// This adapter bridges the network layer (FramePacket) with the
    /// snapshot routing layer (FrameSnapshotDispatcher).
    /// </summary>
    public sealed class FramePacketNetAdapter
    {
        private readonly IFramePacketNetAdapterContext _ctx;

        public FramePacketNetAdapter(IFramePacketNetAdapterContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        /// <summary>
        /// Process a FramePacket: route inputs to buffers and feed snapshots to dispatcher.
        /// </summary>
        public FramePacket ProcessAndFeed(FramePacket packet)
        {
            packet = ProcessInput(packet);
            _ctx.Snapshots?.Feed(packet);
            return packet;
        }

        /// <summary>
        /// Process raw frame data and optional snapshot envelopes.
        /// </summary>
        public FramePacket ProcessAndFeed(
            WorldId worldId,
            FrameIndex frame,
            PlayerInputCommand[] inputs,
            ISnapshotEnvelope[] envelopes)
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

        /// <summary>
        /// Process aggregated input and snapshot frames.
        /// </summary>
        public FramePacket ProcessAndFeed(
            WorldId worldId,
            in RemoteInputFrame inputFrame,
            in RemoteSnapshotFrame snapshotFrame)
        {
            return ProcessAndFeed(worldId, inputFrame.Frame, inputFrame.Commands, snapshotFrame.Envelopes);
        }

        private FramePacket ProcessInput(FramePacket packet)
        {
            if (_ctx.RemoteDrivenWorld == null && _ctx.ConfirmedWorld == null)
                return packet;

            try
            {
                var frame = packet.Frame.Value;
                var worldId = _ctx.RemoteDrivenWorld != null ? _ctx.RemoteDrivenWorld.Id : packet.WorldId;

                PlayerInputCommand[] inputs;
                if (packet.Inputs == null || packet.Inputs.Count == 0)
                {
                    inputs = Array.Empty<PlayerInputCommand>();
                }
                else if (packet.Inputs is PlayerInputCommand[] arr)
                {
                    inputs = arr;
                }
                else
                {
                    inputs = new List<PlayerInputCommand>(packet.Inputs).ToArray();
                }

                if (inputs.Length > 0)
                {
                    Log.Info($"[FramePacketNetAdapter] Buffering authoritative inputs. worldId={worldId.Value}, frame={frame}, count={inputs.Length}, firstOpCode={inputs[0].OpCode}");
                }

                if (_ctx.RemoteDrivenInputSource == null)
                {
                    var delay = _ctx.InputDelayFrames < 0 ? 0 : _ctx.InputDelayFrames;
                    var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delay, MissingFrameMode.FillDefault, Array.Empty<PlayerInputCommand>);
                    _ctx.RemoteDrivenInputSource = buf;
                    _ctx.RemoteDrivenConsumable = buf;
                    _ctx.RemoteDrivenSink = buf;
                }

                if (inputs.Length > 0 && _ctx.RemoteDrivenSink is FrameJitterBuffer<PlayerInputCommand[]> remoteBuffer)
                {
                    var beforeLastConsumed = remoteBuffer.LastConsumedFrame;
                    var beforeTarget = remoteBuffer.TargetFrame;
                    var beforeLate = remoteBuffer.LateCount;
                    var beforeDuplicate = remoteBuffer.DuplicateCount;
                    remoteBuffer.Add(frame, inputs);
                    Log.Info($"[FramePacketNetAdapter] Remote buffer write. frame={frame}, beforeLastConsumed={beforeLastConsumed}, beforeTarget={beforeTarget}, afterTarget={remoteBuffer.TargetFrame}, lateDelta={remoteBuffer.LateCount - beforeLate}, duplicateDelta={remoteBuffer.DuplicateCount - beforeDuplicate}, buffered={remoteBuffer.Count}, containsFrame={remoteBuffer.TryGet(frame, out var stored) && stored != null && stored.Length > 0}");
                }
                else
                {
                    _ctx.RemoteDrivenSink?.Add(frame, inputs);
                }

                if (_ctx.ConfirmedInputSource == null)
                {
                    var buf = new FrameJitterBuffer<PlayerInputCommand[]>(
                        delayFrames: 0,
                        missingMode: MissingFrameMode.FillDefault,
                        missingFrameFactory: Array.Empty<PlayerInputCommand>,
                        initialCapacity: 256);
                    _ctx.ConfirmedInputSource = buf;
                    _ctx.ConfirmedConsumable = buf;
                    _ctx.ConfirmedSink = buf;
                }

                if (inputs.Length > 0 && _ctx.ConfirmedSink is FrameJitterBufferHub<PlayerInputCommand[]> confirmedHub)
                {
                    var beforeLastConsumed = confirmedHub.LastConsumedFrame;
                    var beforeTarget = confirmedHub.TargetFrame;
                    var beforeLate = confirmedHub.LateCount;
                    var beforeDuplicate = confirmedHub.DuplicateCount;
                    confirmedHub.Add(frame, inputs);
                    Log.Info($"[FramePacketNetAdapter] Confirmed buffer write. frame={frame}, beforeLastConsumed={beforeLastConsumed}, beforeTarget={beforeTarget}, afterTarget={confirmedHub.TargetFrame}, lateDelta={confirmedHub.LateCount - beforeLate}, duplicateDelta={confirmedHub.DuplicateCount - beforeDuplicate}, buffered={confirmedHub.Count}, containsFrame={confirmedHub.TryGet(frame, out var stored) && stored != null && stored.Length > 0}");
                }
                else
                {
                    _ctx.ConfirmedSink?.Add(frame, inputs);
                }

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
