using System.Collections.Generic;
using Emilia.Node.Universal.Editor;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 组节点资产
    /// </summary>
    [HideMonoScript]
    public class EditorGroupAsset : UniversalItemAsset
    {
        [LabelText("标题")]
        public string groupTitle = "New Group";

        /// <summary>
        /// 大小
        /// </summary>
        [HideInInspector]
        public Vector2 size;

        /// <summary>
        /// 节点列表
        /// </summary>
        [HideInInspector]
        public List<string> innerNodes = new();

        public override string title => groupTitle;

        public override void SetChildren(List<Object> childAssets)
        {
            base.SetChildren(childAssets);
            innerNodes.Clear();

            int amount = childAssets.Count;
            for (int i = 0; i < amount; i++)
            {
                Object child = childAssets[i];
                if (child == null) continue;
                EditorNodeAsset nodeAsset = child as EditorNodeAsset;
                if (nodeAsset == null) continue;
                this.innerNodes.Add(nodeAsset.id);
            }
        }
    }
}