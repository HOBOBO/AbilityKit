using System;
using System.Collections.Generic;
using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 创建节点面板状态（持久化保存）
    /// </summary>
    [Serializable]
    public class CreateNodeViewState
    {
        public const string CreateNodeViewStateSaveKey = "CreateNodeViewStateSaveKey";

        /// <summary>
        /// 展开的节点
        /// </summary>
        public List<int> expandedIDs = new();

        /// <summary>
        /// 设置展开的节点id
        /// </summary>
        public void SetExpandedIDs(IEnumerable<int> newExpandedIDs)
        {
            this.expandedIDs.Clear();
            this.expandedIDs.AddRange(newExpandedIDs);
        }

        /// <summary>
        /// 保存
        /// </summary>
        public void Save(EditorGraphView graphView)
        {
            graphView.graphLocalSettingSystem.SetTypeSettingValue(CreateNodeViewStateSaveKey, this);
        }

        /// <summary>
        /// 获取保存的实例
        /// </summary>
        public static CreateNodeViewState Get(EditorGraphView graphView)
        {
            CreateNodeViewState createNodeViewState = graphView.graphLocalSettingSystem.GetTypeSettingValue(CreateNodeViewStateSaveKey, new CreateNodeViewState());
            return createNodeViewState;
        }
    }
}