using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class GatewayRoomMembership
    {
        public string RoomId { get; private set; } = string.Empty;
        public ulong NumericRoomId { get; private set; }
        public uint PlayerId { get; private set; }

        public void Commit(string roomId, ulong numericRoomId, uint playerId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new InvalidOperationException("Authoritative room membership has no room id.");
            }
            if (numericRoomId == 0UL)
            {
                throw new InvalidOperationException("Authoritative room membership has no numeric room id.");
            }
            if (playerId == 0u)
            {
                throw new InvalidOperationException("Authoritative room membership has no player id.");
            }

            RoomId = roomId;
            NumericRoomId = numericRoomId;
            PlayerId = playerId;
        }

        public void Clear()
        {
            RoomId = string.Empty;
            NumericRoomId = 0UL;
            PlayerId = 0u;
        }
    }
}
