using System;
using Emilia.Node.Attributes;

namespace Emilia.Node.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public class GraphDataAttribute : Attribute
    {
        public Type graphAssetType;

        public GraphDataAttribute(Type graphAssetType)
        {
            this.graphAssetType = graphAssetType;
        }
    }

    public class GraphData
    {
        public virtual void OnCreate(EditorGraphView graphView) { }
    }

    [GraphData(typeof(EditorGraphAsset))]
    public class BasicGraphData : GraphData
    {
        public GraphSettingStruct graphSetting = GraphSettingStruct.Default;
    }
}