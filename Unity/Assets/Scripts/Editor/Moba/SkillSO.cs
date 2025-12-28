using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Skill", fileName = "SkillCO")]
    public sealed class SkillSO : ScriptableObject
    {
        public SkillData[] dataList;

        [System.Serializable]
        public sealed class SkillData : ISkillCO, IKeyedSO<int>
        {
            [SerializeField] private int _id;
            [SerializeField] private string _name;
            [SerializeField] private int _cooldownMs;
            [SerializeField] private int _range;
            [SerializeField] private int _iconId;
            [SerializeField] private int _category;
            [SerializeField] private int[] _tags;

            public int Key => _id;
            int IMobaConfigObject<int>.Key => _id;

            public string Name => _name;
            public int CooldownMs => _cooldownMs;
            public int Range => _range;
            public int IconId => _iconId;
            public int Category => _category;

            public System.ReadOnlySpan<int> Tags => _tags;

            public int[] GetTagsCopy()
            {
                if (_tags == null) return null;
                var copy = new int[_tags.Length];
                System.Array.Copy(_tags, copy, _tags.Length);
                return copy;
            }
        }
    }
}
