namespace AbilityKit.Ability.Battle.SkillLibrary
{
    public interface ISkillIndex<TKey, TData>
    {
        void OnAdded(TKey key, TData data);
        void OnRemoved(TKey key, TData data);
        void OnUpdated(TKey key, TData oldData, TData newData, SkillUpdate update);
    }
}
