using System;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterGatewaySnapshotDecoder
    {
        private readonly ShooterPureStateSyncDecodeBuffer _pureStateDecodeBuffer = new ShooterPureStateSyncDecodeBuffer();
        private readonly WireStateSyncSnapshotPushDecodeBuffer _wireDecodeBuffer = new WireStateSyncSnapshotPushDecodeBuffer();

        public bool IsSnapshotPush(uint opCode)
        {
            return opCode == RoomGatewayOpCodes.SnapshotPushed || opCode == RoomGatewayOpCodes.DeltaSnapshotPushed;
        }

        public ShooterGatewaySnapshot Decode(ArraySegment<byte> payload)
        {
            var wire = _wireDecodeBuffer.Decode(payload);
            return ShooterGatewaySnapshotMapper.ToGatewaySnapshot(in wire, _pureStateDecodeBuffer);
        }
    }
}
