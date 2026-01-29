namespace AbilityKit.Protocol
{
    public readonly struct MessageEnvelope
    {
        public readonly int MessageType;
        public readonly string Payload;

        public MessageEnvelope(int messageType, string payload)
        {
            MessageType = messageType;
            Payload = payload;
        }
    }
}
