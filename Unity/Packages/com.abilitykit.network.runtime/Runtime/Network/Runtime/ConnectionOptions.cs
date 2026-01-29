using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Runtime
{
    public sealed class ConnectionOptions
    {
        public IFrameCodec FrameCodec;

        public TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
        public TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(30);

        public bool EnableReconnect = true;
        public TimeSpan ReconnectInitialDelay = TimeSpan.FromSeconds(1);
        public TimeSpan ReconnectMaxDelay = TimeSpan.FromSeconds(10);
        public double ReconnectBackoffMultiplier = 1.5;
        public int ReconnectMaxAttempts = -1;

        public int MaxFrameLength = 4 * 1024 * 1024;

        public uint HeartbeatOpCode = 0;

        public bool EnableKickHandling = true;
        public uint KickPushOpCode = 9000;
    }
}
