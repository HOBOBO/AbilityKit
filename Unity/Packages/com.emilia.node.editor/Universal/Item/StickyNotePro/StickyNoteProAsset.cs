using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 便利贴（支持MarkDown格式）节点资产
    /// </summary>
    public class StickyNoteProAsset : UniversalItemAsset
    {
        [HideLabel, TextArea(50, 50)]
        public string context = "内容";

        public override string title => "便利贴";
    }
}