namespace AbilityKit.Triggering.Runtime
{
    public readonly struct TriggerContext
    {
        public readonly int Frame;
        public readonly int Sequence;

        public TriggerContext(int frame, int sequence)
        {
            Frame = frame;
            Sequence = sequence;
        }
    }
}
