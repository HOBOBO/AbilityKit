using System;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Client
{
    public static class GatewayRoomSdkConnectionPreset
    {
        public static void Configure(ConnectionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.FrameCodec = LengthPrefixedFrameCodec.Instance;
            options.EnableKickHandling = true;
            options.KickPushOpCode = RoomGatewayOpCodes.SessionKicked;
            options.EnableReconnect = true;
            options.ReconnectInitialDelay = TimeSpan.FromSeconds(1);
            options.ReconnectMaxDelay = TimeSpan.FromSeconds(15);
            options.ReconnectBackoffMultiplier = 2d;
            options.ReconnectMaxAttempts = ReconnectBackoffPolicy.MaxAttempts;
        }
    }
}
