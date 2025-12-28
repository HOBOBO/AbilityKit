using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Character", fileName = "CharacterCO")]
    public sealed class CharacterSO : ScriptableObject
    {
        public CharacterData[] dataList;

        [System.Serializable]
        public sealed class CharacterData : ICharacterCO, IKeyedSO<int>
        {
            [SerializeField] private int _id;
            [SerializeField] private string _name;
            [SerializeField] private int _modelId;
            [SerializeField] private int _attributeTemplateId;
            [SerializeField] private int[] _skillIds;

            public int Key => _id;
            int IMobaConfigObject<int>.Key => _id;

            public string Name => _name;
            public int ModelId => _modelId;
            public int AttributeTemplateId => _attributeTemplateId;
            public int[] SkillIds => _skillIds;
        }
    }
}
