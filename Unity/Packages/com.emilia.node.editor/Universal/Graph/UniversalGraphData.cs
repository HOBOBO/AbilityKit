using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    [GraphData(typeof(EditorUniversalGraphAsset))]
    public class UniversalGraphData : GraphData
    {
        public UniversalGraphSetting graphSetting;
    }
}