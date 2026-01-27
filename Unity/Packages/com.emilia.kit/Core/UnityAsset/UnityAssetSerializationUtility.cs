#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public static class UnityAssetSerializationUtility
    {
        public static UnityAssetSerializationPack SerializeUnityAsset(IUnityAsset unityAsset)
        {
            UnityAssetSerializationPack root = new UnityAssetSerializationPack();

            Object unityObject = unityAsset as Object;
            root.type = unityAsset.GetType();
            UnitySerializationUtility.SerializeUnityObject(unityObject, ref root.data, ref root.unityObjects, DataFormat.Binary, true);
            root.children = new List<UnityAssetSerializationPack>();

            SerializeUnityAsset(unityAsset, root);

            return root;
        }

        private static void SerializeUnityAsset(IUnityAsset unityAsset, UnityAssetSerializationPack parent)
        {
            List<Object> childAssets = unityAsset.GetChildren();
            if (childAssets == null || childAssets.Count == 0) return;

            for (var i = 0; i < childAssets.Count; i++)
            {
                Object childAsset = childAssets[i];
                UnityAssetSerializationPack child = new UnityAssetSerializationPack();
                parent.children.Add(child);
                child.type = child.GetType();
                UnitySerializationUtility.SerializeUnityObject(childAsset, ref child.data, ref child.unityObjects, DataFormat.Binary, true);
                child.children = new List<UnityAssetSerializationPack>();

                if (child is IUnityAsset unityChildAsset) SerializeUnityAsset(unityChildAsset, child);
            }
        }

        public static T DeserializeUnityAsset<T>(UnityAssetSerializationPack root) where T : class, IUnityAsset
        {
            if (root == null) return default;

            List<Object> childAssets = new List<Object>();

            for (var i = 0; i < root.children.Count; i++)
            {
                UnityAssetSerializationPack child = root.children[i];
                IUnityAsset childUnityAsset = DeserializeUnityAsset<IUnityAsset>(child);
                childAssets.Add(childUnityAsset as Object);
            }

            IUnityAsset unityAsset = ScriptableObject.CreateInstance(root.type) as IUnityAsset;
            if (unityAsset == null) return default;

            Object unityObject = unityAsset as Object;
            UnitySerializationUtility.DeserializeUnityObject(unityObject, ref root.data, ref root.unityObjects, DataFormat.Binary);
            unityAsset.SetChildren(childAssets);

            return unityAsset as T;
        }
    }
}
#endif