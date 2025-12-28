using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/BattleAttributeTemplate", fileName = "BattleAttributeTemplateCO")]
    public sealed class BattleAttributeTemplateSO : ScriptableObject
    {
        public BattleAttributeTemplateData[] dataList;

        [System.Serializable]
        public sealed class BattleAttributeTemplateData : IBattleAttributeTemplateCO, IKeyedSO<int>
        {
            [SerializeField] private int _id;
            [SerializeField] private int _maxHp;
            [SerializeField] private int _attack;
            [SerializeField] private int _defense;
            [SerializeField] private int _moveSpeed;

            public int Key => _id;
            int IMobaConfigObject<int>.Key => _id;

            public int MaxHp => _maxHp;
            public int Attack => _attack;
            public int Defense => _defense;
            public int MoveSpeed => _moveSpeed;
        }
    }
}
