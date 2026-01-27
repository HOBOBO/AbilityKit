#if UNITY_EDITOR
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    /// <summary>
    /// 创建资源选择器
    /// </summary>
    public static class OdinMenuFactory
    {
        public static OdinMenuTypeBuilder Type<T>() => new(typeof(T));

        public static OdinMenuTypeBuilder Type(Type baseType) => new(baseType);

        public static OdinMenuBuilder<T1, T2> ScriptableObject<T1, T2>() where T1 : ScriptableObject
        {
            return new OdinMenuBuilder<T1, T2>()
                .WithResources(EditorAssetKit.GetEditorResources<T1>())
                .WithDescription((x) => ObjectDescriptionUtility.GetDescription(x));
        }

        public static OdinMenuBuilder<T1, T2> ScriptableObjectAtPath<T1, T2>(string path) where T1 : ScriptableObject
        {
            return new OdinMenuBuilder<T1, T2>()
                .WithResources(EditorAssetKit.LoadAssetAtPath<T1>(path))
                .WithDescription((x) => ObjectDescriptionUtility.GetDescription(x));
        }

        public static OdinMenuBuilder<GameObject, T> Prefab<T>(string path) =>
            new OdinMenuBuilder<GameObject, T>()
                .WithResources(EditorAssetKit.LoadAtPath<GameObject>(path, "*.prefab"))
                .WithDescription((x) => ObjectDescriptionUtility.GetGameObjectDescription(x));

        public static OdinMenuBuilder<T1, T2> Asset<T1, T2>(string path, string searchPattern) where T1 : Object
        {
            return new OdinMenuBuilder<T1, T2>()
                .WithResources(EditorAssetKit.LoadAtPath<T1>(path, searchPattern))
                .WithDescription((x) => ObjectDescriptionUtility.GetDescription(x));
        }
    }
}
#endif