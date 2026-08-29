namespace AbilityKit.Protocol.Moba.Generated.GatewayFrameSync
{
    public static class OpCodes
    {
        public const uint SubmitFrameInput = 2001;
        public const uint CatchUpRequest = 2002;
        public const uint GetMetricsRequest = 2003;
        public const uint SpectatorSubscribe = 2004;
        public const uint FramePushed = 9001;
        public const uint CatchUpPayloadPush = 9010;
        // Frame-sync metrics are returned as the correlated response to GetMetricsRequest.
        public const uint MetricsResponse = GetMetricsRequest;
    }
}
