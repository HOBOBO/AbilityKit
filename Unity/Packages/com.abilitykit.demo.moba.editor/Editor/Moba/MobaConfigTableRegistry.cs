#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaConfigTableRegistry
    {
        public static Type[] TableAssetTypes
        {
            get
            {
                var result = new List<Type>(16);
                foreach (var t in TypeCache.GetTypesDerivedFrom<MobaConfigTableAssetSO>())
                {
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    result.Add(t);
                }

                return result.ToArray();
            }
        }
    }
}
#endif
