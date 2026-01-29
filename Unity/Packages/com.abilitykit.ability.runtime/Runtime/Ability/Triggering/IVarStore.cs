namespace AbilityKit.Ability.Triggering
{
    public interface IVarStore
    {
        bool TryGet(string key, out object value);
        bool TryGet<T>(string key, out T value);
        void Set(string key, object value);
    }
}
