//------------------------------------------------------------
//        File:  NodeData.cs
//       Brief:  NodeData
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-17
//============================================================

using System.Collections.Generic;
using System.Runtime.Serialization;
using BTCore.Runtime.OtherNodes;

namespace BTCore.Runtime
{
    public class BTData
    {
        public EntryNode EntryNode;
        public readonly List<BTNode> Nodes = new();

        private readonly Dictionary<string, BTNode> _guid2Nodes = new();
        
#if UNITY_EDITOR        
        public readonly List<StickNoteNode> StickNotes = new();
        public readonly List<GroupNode> NodeGroups = new();

        public void AddNode(BTNode node) {
            Nodes.Add(node);
            _guid2Nodes.Add(node.Guid, node);
        }

        public void RemoveNode(BTNode node) {
            if (!_guid2Nodes.ContainsKey(node.Guid)) {
                return;
            }

            var toRemove = _guid2Nodes[node.Guid];
            Nodes.Remove(toRemove);
            _guid2Nodes.Remove(node.Guid);
        }
#endif

        /// <summary>
        /// 节点替换，主要用于外部节点处理
        /// </summary>
        /// <param name="index">位于Nodes列表中的索引</param>
        /// <param name="newNode">新的外部节点</param>
        public void ReplaceNode(int index, BTNode newNode) {
            if (index < 0 || index >= Nodes.Count) {
                return;
            }
            
            Nodes[index] = newNode;
            _guid2Nodes[newNode.Guid] = newNode;
        }

        public BTNode GetNodeByGuid(string guid) {
            return _guid2Nodes.ContainsKey(guid) ? _guid2Nodes[guid] : null;
        }
        
        // TODO 其他序列化可能不会触发回调
        [OnDeserialized]
        private void OnAfterDeserialize(StreamingContext context) {
            Nodes.ForEach(node => {
                if (node is EntryNode entryNode) {
                    EntryNode = entryNode;
                }
                _guid2Nodes.Add(node.Guid, node);
            });
        }
    }
}