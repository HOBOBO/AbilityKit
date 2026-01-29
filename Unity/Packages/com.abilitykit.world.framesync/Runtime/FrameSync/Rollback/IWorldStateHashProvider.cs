namespace AbilityKit.Ability.FrameSync.Rollback
{
    public readonly struct WorldStateHash
    {
        public readonly uint Value;

        public WorldStateHash(uint value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }

    public interface IWorldStateHashProvider
    {
        bool TryGetHash(FrameIndex frame, out WorldStateHash hash);
    }
}
