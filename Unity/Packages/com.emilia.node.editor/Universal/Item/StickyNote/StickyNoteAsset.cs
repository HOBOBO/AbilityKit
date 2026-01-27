using Sirenix.OdinInspector;
using UnityEditor.Experimental.GraphView;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 便利贴节点资产
    /// </summary>
    [HideMonoScript]
    public class StickyNoteAsset : UniversalItemAsset
    {
        [LabelText("标题")]
        public string stickyTitle = "Title";

        [LabelText("内容")]
        public string content = "Description";

        [LabelText("字体大小")]
        public StickyNoteFontSize fontSize = StickyNoteFontSize.Small;

        [LabelText("主题")]
        public StickyNoteTheme theme = StickyNoteTheme.Black;

        public override string title => stickyTitle;
    }
}