namespace AbilityKit.Ability.Triggering.Blackboard
{
    public interface IBlackboard
    {
        bool TryGet(string key, out object value);

        bool TryGet<T>(string key, out T value);

        bool TryGetDouble(string key, out double value);

        void Set(string key, object value);

        IBlackboard CloneShallow();
    }
}
