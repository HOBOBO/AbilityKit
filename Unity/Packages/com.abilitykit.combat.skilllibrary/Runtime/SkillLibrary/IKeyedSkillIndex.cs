using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.SkillLibrary
{
    public interface IKeyedSkillIndex<TIndexKey, TKey>
    {
        IReadOnlyCollection<TKey> Get(TIndexKey key);
        bool TryGet(TIndexKey key, out IReadOnlyCollection<TKey> skills);
    }
}
