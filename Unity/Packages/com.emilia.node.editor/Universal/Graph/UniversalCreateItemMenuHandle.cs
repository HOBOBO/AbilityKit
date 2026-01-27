using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用创建Item菜单处理
    /// </summary>
    [EditorHandle(typeof(EditorUniversalGraphAsset))]
    public class UniversalCreateItemMenuHandle : CreateItemMenuHandle
    {
        public override void CollectItemMenus(EditorGraphView graphView, List<CreateItemMenuInfo> itemTypes)
        {
            base.CollectItemMenus(graphView, itemTypes);
            CreateItemMenuInfo group = new();
            group.itemAssetType = typeof(EditorGroupAsset);
            group.path = "Group";

            itemTypes.Add(group);

            CreateItemMenuInfo sticky = new();
            sticky.itemAssetType = typeof(StickyNoteAsset);
            sticky.path = "Sticky Note";

            itemTypes.Add(sticky);

            CreateItemMenuInfo stickyPro = new();
            stickyPro.itemAssetType = typeof(StickyNoteProAsset);
            stickyPro.path = "Sticky Note Pro";

            itemTypes.Add(stickyPro);
        }
    }
}