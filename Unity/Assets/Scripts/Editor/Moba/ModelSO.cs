using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Model", fileName = "ModelCO")]
    public sealed class ModelSO : ScriptableObject
    {
        public ModelData[] dataList;

        [System.Serializable]
        public sealed class ModelData : IModelCO, IKeyedSO<int>
        {
            [SerializeField] private int _id;
            [SerializeField] private string _prefabPath;
            [SerializeField] private float _scale = 1f;

            public int Key => _id;
            int IMobaConfigObject<int>.Key => _id;

            public string PrefabPath => _prefabPath;
            public float Scale => _scale;
        }
    }
}
