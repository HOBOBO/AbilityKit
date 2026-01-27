#if UNITY_EDITOR
using System.Collections.Generic;

namespace Emilia.Kit
{
    public static class HierarchyAssetExtension
    {
        public static IHierarchyAsset GetRootAsset(this IHierarchyAsset hierarchyAsset)
        {
            IHierarchyAsset rootGraphAsset = hierarchyAsset;
            while (rootGraphAsset.parent != null) rootGraphAsset = rootGraphAsset.parent;
            return rootGraphAsset;
        }

        public static void SendMessageToParent(this IHierarchyAsset hierarchyAsset, string message, object arg = null)
        {
            IHierarchyAsset parent = hierarchyAsset;
            while (parent != null)
            {
                IHierarchyAssetMessageHandle handle = HierarchyAssetMessageHandleUtility.GetHandle(parent.GetType(), message);
                if (handle != null)
                {
                    handle.MessageHandle(parent, hierarchyAsset, arg);
                    return;
                }

                parent = parent.parent;
            }
        }

        public static void SendMessageToChildrenForeword(this IHierarchyAsset hierarchyAsset, string message, object arg = null)
        {
            Queue<IHierarchyAsset> queue = new();
            queue.Enqueue(hierarchyAsset);
            while (queue.Count > 0)
            {
                IHierarchyAsset currentAsset = queue.Dequeue();
                IHierarchyAssetMessageHandle handle = HierarchyAssetMessageHandleUtility.GetHandle(currentAsset.GetType(), message);
                if (handle != null) handle.MessageHandle(currentAsset, hierarchyAsset, arg);

                IReadOnlyList<IHierarchyAsset> children = currentAsset.children;
                if (children == null) continue;
                int amount = children.Count;
                for (int i = 0; i < amount; i++)
                {
                    IHierarchyAsset child = children[i];
                    if (child == null) continue;
                    queue.Enqueue(child);
                }
            }
        }

        public static void SendMessageToChildrenBackword(this IHierarchyAsset hierarchyAsset, string message, object arg = null)
        {
            Queue<IHierarchyAsset> queue = new();
            queue.Enqueue(hierarchyAsset);
            while (queue.Count > 0)
            {
                IHierarchyAsset currentAsset = queue.Dequeue();
                IReadOnlyList<IHierarchyAsset> children = currentAsset.children;
                if (children == null) continue;
                int amount = children.Count;
                for (int i = 0; i < amount; i++)
                {
                    IHierarchyAsset child = children[i];
                    if (child == null) continue;
                    queue.Enqueue(child);
                }

                IHierarchyAssetMessageHandle handle = HierarchyAssetMessageHandleUtility.GetHandle(currentAsset.GetType(), message);
                if (handle != null) handle.MessageHandle(currentAsset, hierarchyAsset, arg);
            }
        }
    }
}
#endif