namespace AbilityKit.Ability.Battle.EntityManager
{
    public readonly struct SetKeyUpdate<TKey>
    {
        public readonly int UpdateType;
        public readonly TKey Key;

        public SetKeyUpdate(int updateType, TKey key)
        {
            UpdateType = updateType;
            Key = key;
        }
    }

    public readonly struct AddKeyUpdate<TKey>
    {
        public readonly int UpdateType;
        public readonly TKey Key;

        public AddKeyUpdate(int updateType, TKey key)
        {
            UpdateType = updateType;
            Key = key;
        }
    }

    public readonly struct RemoveKeyUpdate<TKey>
    {
        public readonly int UpdateType;
        public readonly TKey Key;

        public RemoveKeyUpdate(int updateType, TKey key)
        {
            UpdateType = updateType;
            Key = key;
        }
    }
}
