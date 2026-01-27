#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public static class IUnityAssetExtension
    {
        public static List<Object> CollectAsset(this IUnityAsset unityAsset)
        {
            List<Object> allAsset = new List<Object>();

            Queue<IUnityAsset> queue = new Queue<IUnityAsset>();
            queue.Enqueue(unityAsset);

            while (queue.Count > 0)
            {
                IUnityAsset currentAsset = queue.Dequeue();
                Object unityObject = currentAsset as Object;
                if (unityObject != null) allAsset.Add(unityObject);

                List<Object> childAssets = currentAsset.GetChildren();
                if (childAssets == null) continue;
                int amount = childAssets.Count;
                for (int i = 0; i < amount; i++)
                {
                    Object child = childAssets[i];
                    if (child == null) continue;
                    IUnityAsset childAsset = child as IUnityAsset;
                    if (childAsset != null) queue.Enqueue(childAsset);
                    else allAsset.Add(child);
                }
            }

            return allAsset;
        }

        public static void SaveAll(this IUnityAsset unityAsset)
        {
            unityAsset.SetDirtyAll();
            AssetDatabase.SaveAssets();
        }
        
        public static void SetDirtyAll(this IUnityAsset unityAsset)
        {
            List<Object> allAsset = unityAsset.CollectAsset();
            int count = allAsset.Count;
            for (int i = 0; i < count; i++)
            {
                Object asset = allAsset[i];
                EditorUtility.SetDirty(asset);
            }
        }

        public static void PasteChild(this IUnityAsset asset)
        {
            List<Object> pasteList = new List<Object>();
            List<Object> childAssets = asset.GetChildren();

            if (childAssets != null)
            {
                int amount = childAssets.Count;
                for (int i = 0; i < amount; i++)
                {
                    Object child = childAssets[i];
                    if (child == null) continue;
                    Object pasteChild = Object.Instantiate(child);
                    pasteChild.name = child.name;

                    Undo.RegisterCreatedObjectUndo(pasteChild, "Paste");

                    IUnityAsset childAsset = pasteChild as IUnityAsset;
                    if (childAsset != null) PasteChild(childAsset);

                    pasteList.Add(pasteChild);
                }
            }

            asset.SetChildren(pasteList);
        }
    }
}
#endif