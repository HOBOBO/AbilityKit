namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Platform-neutral actor transform using presentation coordinates: X right, Y up, Z forward.
    /// </summary>
    public readonly struct ActorTransformData
    {
        public int ActorId { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public float PositionZ { get; }
        public float ForwardX { get; }
        public float ForwardY { get; }
        public float ForwardZ { get; }
        public float RotationY { get; }
        public float Scale { get; }

        public ActorTransformData(
            int actorId,
            float x,
            float y,
            float z,
            float forwardX,
            float forwardY,
            float forwardZ,
            float rotationY,
            float scale)
        {
            ActorId = actorId;
            PositionX = x;
            PositionY = y;
            PositionZ = z;
            ForwardX = forwardX;
            ForwardY = forwardY;
            ForwardZ = forwardZ;
            RotationY = rotationY;
            Scale = scale;
        }

        public ActorTransformData(
            int actorId,
            float x,
            float y,
            float z,
            float rotationY,
            float scale)
            : this(
                actorId,
                x,
                y,
                z,
                forwardX: 0f,
                forwardY: 0f,
                forwardZ: 1f,
                rotationY,
                scale)
        {
        }
    }
}
