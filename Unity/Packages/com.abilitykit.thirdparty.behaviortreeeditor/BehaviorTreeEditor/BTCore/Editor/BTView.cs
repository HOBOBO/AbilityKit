//------------------------------------------------------------
//        File:  BTEditor.cs
//       Brief:  BTEditor
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-02
//============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using BTCore.Runtime;
using BTCore.Runtime.Composites;
using BTCore.Runtime.Decorators;
using BTCore.Runtime.OtherNodes;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTCore.Editor
{
    public class BTView : GraphView, IDataSerializable<BTData>
    {
        public new class UxmlFactory : UxmlFactory<BTView, UxmlTraits> { }

        private BTData _btData;

        public Action<BTNodeView> OnNodeSelected;

        private readonly Vector2 _pasteNodeOffset = new Vector2(50f, 50f);
        private readonly Vector2 _entryPos = new Vector2(225f, 150f);
        private readonly List<GraphElement> _graphElements = new();
        private bool _isRemoveOnly = false;

        private Image _abortIcon;

        public BTView() {
            Insert(0, new GridBackground());
            
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            RegisterCopyAndPasteEvent();
            
            // 添加BTView样式，主要是格子背景展示
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BTEditorDef.BTViewStylePath);
            styleSheets.Add(styleSheet);
        }

        private void RegisterCopyAndPasteEvent() {
            serializeGraphElements = elements => {
                var copyPasteData = new CopyPasteData();
                copyPasteData.AddGraphElements(elements);
                return JsonConvert.SerializeObject(copyPasteData);
            };

            unserializeAndPaste = (_, data) => {
                var copyPasteData = JsonConvert.DeserializeObject<CopyPasteData>(data);
                if (copyPasteData == null || copyPasteData.Guids.Count <= 0) {
                    return;
                }
                
                ClearSelection();
                var oldData = JsonConvert.SerializeObject(_btData, BTDef.SerializerSettingsAll);
                
                // 找到所有复制的节点
                var nodesToCopy = new List<BTNodeView>();
                copyPasteData.Guids.ForEach(guid => {
                    nodesToCopy.Add(FindNodeView(guid));
                });

                // 找到待复制节点之间的连线关系
                var edgesToCreate = new List<EdgeToCreate>();
                foreach (var nodeView in nodesToCopy) {
                    var nodeParent = nodeView.NodeParent;
                    if (nodeParent == null || !nodesToCopy.Contains(nodeParent)) {
                        continue;
                    }

                    edgesToCreate.Add(new EdgeToCreate() {
                        ParentGuid = nodeParent.Node.Guid,
                        ChildGuid = nodeView.Node.Guid
                    });
                }
                
                // 记录旧节点Guid -> 新节点Guid映射关系
                var guidsMapping = new Dictionary<string, string>();
                // 根据待复制节点列表，创建新的节点数据和节点视图
                foreach (var nodeView in nodesToCopy) {
                    var newPos = new Vector2(nodeView.Node.PosX, nodeView.Node.PosY) + _pasteNodeOffset;
                    var newNode = CreateNode(nodeView.Node.GetType(), newPos);
                    var newNodeView = CreateNodeView(newNode);
                    AddToSelection(newNodeView);
                    
                    guidsMapping.Add(nodeView.Node.Guid, newNode.Guid);
                }

                // 建立新节点之间的数据跟视图的连接关系
                foreach (var toCreate in edgesToCreate) {
                    var newParent = FindNodeView(guidsMapping[toCreate.ParentGuid]);
                    var newChild = FindNodeView(guidsMapping[toCreate.ChildGuid]);
                    if (newParent == null || newChild == null) {
                        continue;
                    }
                    
                    AddChild(newParent.Node, newChild.Node);
                    AddChildView(newParent, newChild);
                }
                
                var newData = JsonConvert.SerializeObject(_btData, BTDef.SerializerSettingsAll);
                var command = new NodeDataCommand(this, oldData, newData);
                BTEditorWindow.Instance.AddCommand(command);
            };
        }
        
        public void ImportData(BTData btData) {
            _btData = btData;
            
            ClearGraphs();
            _graphElements.Clear();
            BTEditorWindow.Instance.ClearNodeSelectedInspector();
            
            // 默认没有会创建对应的入口节点
            _btData.EntryNode ??= CreateNode<EntryNode>(_entryPos) as EntryNode;

            var treeNodes = btData.Nodes;
            // 根据保存的数据创建对应节点
            treeNodes.ForEach(node => CreateNodeView(node));
            // 根据节点数据关系创建连线
            treeNodes.ForEach(node => {
                var childrenGuids = node.GetChildrenGuids();
                childrenGuids.ForEach(guid => {
                    var parentView = FindNodeView(node.Guid);
                    var childView = FindNodeView(guid);
                    if (parentView != null && childView != null) {
                        ConnectNode(parentView, childView);
                    }
                    
                });
            });

            // 处理StickNote部分
            foreach (var stickNoteData in _btData.StickNotes) {
                var stickyNote = new StickyNote
                {
                    title = stickNoteData.Title,
                    contents = stickNoteData.Content
                };
                stickyNote.SetPosition(new Rect(stickNoteData.X, stickNoteData.Y, stickNoteData.Width, stickNoteData.Height));
                AddGraphElement(stickyNote);
                AddElement(stickyNote);
            }
            
            // 处理Group部分
            foreach (var groupData in _btData.NodeGroups) {
                var colorGroup = new ColorGroup {
                    title = groupData.Title,
                    style = { backgroundColor = new StyleColor(groupData.GroupColor) }
                };
                colorGroup.SetPosition(new Rect(groupData.X, groupData.Y, groupData.Width, groupData.Height));

                foreach (var nodeGuid in groupData.NodeGuids) {
                    var node = GetNodeByGuid(nodeGuid);
                    if (node != null) {
                        colorGroup.AddElement(node);
                    }
                }
                
                AddGraphElement(colorGroup);
                AddElement(colorGroup);
            }
        }

        private void ClearGraphs() {
            graphViewChanged -= OnGraphViewChanged;
            DeleteElements(graphElements);
            graphViewChanged += OnGraphViewChanged;
        }

        private BTNodeView FindNodeView(string guid) {
            return GetNodeByGuid(guid) as BTNodeView;
        }

        public List<GraphElement> GetGraphElements() {
            return _graphElements;
        }
        
        public void AddGraphElement(GraphElement element) {
            _graphElements.Add(element);
        }

        public void RemoveGraphElement(GraphElement element) {
            _graphElements.Remove(element);
        }
        
        public BTData ExportData() {
            _btData.StickNotes.Clear();
            _btData.NodeGroups.Clear();
            
            // 导出数据的时候，需要将GraphElements里的数据保存下
            foreach (var graphElement in GetGraphElements()) {
                switch (graphElement) {
                    case StickyNote stickyNote:
                        ExportStickNoteData(stickyNote);
                        break;
                    case ColorGroup colorGroup:
                        ExportNodeGroupData(colorGroup);
                        break;
                }
            }
            
            return _btData;
        }

        private void ExportStickNoteData(StickyNote stickyNote) {
            _btData.StickNotes.Add(new StickNoteNode() {
                Title = stickyNote.title,
                Content = stickyNote.contents,
                X = stickyNote.GetPosition().x,
                Y = stickyNote.GetPosition().y,
                Width = stickyNote.GetPosition().width,
                Height = stickyNote.GetPosition().height
            });
        }

        private void ExportNodeGroupData(ColorGroup colorGroup) {
            var color = colorGroup.style.backgroundColor.value;
            var groupNode = new GroupNode() {
                Title = colorGroup.title,
                X = colorGroup.GetPosition().x,
                Y = colorGroup.GetPosition().y,
                Width = colorGroup.GetPosition().width,
                Height = colorGroup.GetPosition().height,
                GroupColor = new GroupColor(color)
            };
            
            // 查询当前组包含那些节点
            foreach (var element in colorGroup.containedElements) {
                if (element is BTNodeView nodeView) {
                    groupNode.NodeGuids.Add(nodeView.Node.Guid);
                }
            }
            
            _btData.NodeGroups.Add(groupNode);
        }

        /// <summary>
        /// 右键打开，节点搜索框
        /// </summary>
        /// <param name="evt"></param>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            NodeSearchWindow.Show(evt.mousePosition, null);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange) {
            if (_isRemoveOnly) {
                return graphViewChange;
            }

            var oldData = JsonConvert.SerializeObject(_btData, BTDef.SerializerSettingsAll);
            var toRemove = graphViewChange.elementsToRemove;
            
            toRemove?.ForEach(ele => {
                // 连线被删除，更新BT数据部分之间的连接关系
                if (ele is Edge edge) {
                    if (edge.output.node is BTNodeView parentView && edge.input.node is BTNodeView childView) {
                        RemoveChild(parentView.Node, childView.Node);
                    }
                }
                // 节点被删除
                if (ele is BTNodeView nodeView) {
                    var foundNode = _btData.GetNodeByGuid(nodeView.Node.Guid);
                    if (foundNode != null) {
                        _btData.RemoveNode(foundNode);
                    }
                }

                // 其他节点
                if (ele is StickyNote or ColorGroup) {
                    RemoveGraphElement(ele);
                }
            });

            // 连线被创建，更新BT数据部分之间的连接关系
            var edgesToCreate = graphViewChange.edgesToCreate;
            edgesToCreate?.ForEach(edge => {
                if (edge.output.node is BTNodeView parentView && edge.input.node is BTNodeView childView) {
                    AddChild(parentView.Node, childView.Node);
                }
            });

            // 节点被删除 or 连线被删除 or 连线被创建 -> 都需要创建记录数据
            if (toRemove is {Count: > 0} || edgesToCreate is {Count: > 0}) {
                var newData = JsonConvert.SerializeObject(_btData, BTDef.SerializerSettingsAll);
                var command = new NodeDataCommand(this, oldData, newData);
                BTEditorWindow.Instance.AddCommand(command);
            }
            
            // 节点被移动，对于复合节点，需要重新排序Children的顺序关系
            if (graphViewChange.movedElements != null) {
                nodes.ForEach(node => {
                    if (node is BTNodeView nodeView) {
                        nodeView.SortChildren();
                    }
                });
            }
            
            return graphViewChange;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) {
            return ports.Where(port => port.node != startPort.node && port.direction != startPort.direction).ToList();
        }

        public void CreteNode(Type type, Vector2 pos, BTNodeView sourceNode, bool isAsParent) {
            var nodeView = (BTNodeView) null;
            var oldData = JsonConvert.SerializeObject(_btData, BTDef.SerializerSettingsAll);

            if (!type.IsSubclassOf(typeof(BTNode)) && Activator.CreateInstance(type) is GraphElement element) {
                element.style.left = pos.x;
                element.style.top = pos.y;
                AddGraphElement(element);
                AddElement(element);
                return;
            }
            
            var node = CreateNode(type, pos);

            // sourceNode作为子节点
            if (sourceNode != null && !isAsParent) {
                // 刪除此节点在数据部分作为孩子的关系
                foreach (var connection in sourceNode.Input.connections) {
                    if (connection.output.node is BTNodeView parentView) {
                        RemoveChild(parentView.Node, sourceNode.Node);
                    }
                }
                // 重新构建数据关系
                AddChild(node, sourceNode.Node);
                // 更新对应的节点视图
                nodeView = CreateNodeView(node);
                AddChildView(nodeView, sourceNode);
            }
            else {
                if (sourceNode != null) {
                    AddChild(sourceNode.Node, node);
                }
                
                // 更新对应的节点视图
                nodeView = CreateNodeView(node);
                if (sourceNode != null) {
                    AddChildView(sourceNode, nodeView);
                }
            }
            
            var newData = JsonConvert.SerializeObject(_btData, BTDef.SerializerSettingsAll);
            var command = new NodeDataCommand(this, oldData, newData);
            BTEditorWindow.Instance.AddCommand(command);
            
            // 选中创建的节点
            SelectNode(nodeView);
        }

        private void CreateOtherNode(Type type) {
            
        }
        
        private BTNode CreateNode<T>(Vector2 pos) where T : BTNode {
            return CreateNode(typeof(T), pos);
        }

        private BTNode CreateNode(Type type, Vector2 pos) {
            if (Activator.CreateInstance(type) is not BTNode node) {
                return null;
            }

            node.Name = type.Name;
            node.Guid = Guid.NewGuid().ToString();
            node.PosX = pos.x;
            node.PosY = pos.y;
            
            _btData.AddNode(node);
            return node;
        }
        
        private BTNodeView CreateNodeView(BTNode btNode) {
            var nodeView = new BTNodeView(this);
            nodeView.OnNodeSelected = OnNodeSelected;
            nodeView.ImportData(btNode);
            
            if (btNode is EntryNode) {
                nodeView.capabilities &= ~Capabilities.Deletable;
                nodeView.capabilities &= ~Capabilities.Movable;
            }

            AddElement(nodeView);
            return nodeView;
        }
        
        private void AddChildView(BTNodeView parentView, BTNodeView childView) {
            // 父节点输出端口为单连接时，需要先删除已存在的连线
            if (parentView.Output.capacity == Port.Capacity.Single) {
                RemoveEdgesOnly(parentView.Output.connections);
            }
            // 删除子节点输入端口已存在的连线
            RemoveEdgesOnly(childView.Input.connections);
            // 连接两个节点
            ConnectNode(parentView, childView);
        }

        private void RemoveEdgesOnly(IEnumerable<Edge> toRemove) {
            _isRemoveOnly = true;
            
            // 删除连线前，需更新数据部分关系
            var edgeToRemove = toRemove.ToList();
            foreach (var edge in edgeToRemove) {
                if (edge.output.node is BTNodeView parentView && edge.input.node is BTNodeView childView) {
                    RemoveChild(parentView.Node, childView.Node);
                }
            }
            DeleteElements(edgeToRemove);

            _isRemoveOnly = false;
        }

        private void ConnectNode(BTNodeView parentView, BTNodeView childView) {
            var edge = parentView.Output.ConnectTo(childView.Input);
            AddElement(edge);
        }
        
        public void SelectNode(BTNodeView nodeView) {
            ClearSelection();
            AddToSelection(nodeView);
        }

        public void UpdateNodesStyle() {
            nodes.ForEach(node => {
                var nodeView = node as BTNodeView;
                nodeView?.UpdateStyleByState();
            });
        }

        private void AddChild(BTNode parentNode, BTNode childNode) {
            switch (parentNode) {
                case ParentNode pNode: {
                    pNode.ChildrenGuids.Add(childNode.Guid);
                    break;
                }
                case EntryNode entryNode: {
                    entryNode.ChildGuid = childNode.Guid;
                    break;
                }
            }
        }

        private void RemoveChild(BTNode parentNode, BTNode childNode) {
            switch (parentNode) {
                case ParentNode pNode: {
                    pNode.ChildrenGuids.Remove(childNode.Guid);
                    break;
                }
                case EntryNode entryNode: {
                    entryNode.ChildGuid = null;
                    break;
                }
            }
        }

        private class CopyPasteData
        {
            public readonly List<string> Guids = new List<string>();

            public void AddGraphElements(IEnumerable<GraphElement> toCopy) {
                foreach (var element in toCopy) {
                    if (element is BTNodeView { Node: not EntryNode } nodeView) {
                        Guids.Add(nodeView.Node.Guid);
                    }
                }
            }
        }
        
        /// <summary>
        /// 只记录有连接关系节点的Guid即可
        /// </summary>
        private class EdgeToCreate
        {
            public string ParentGuid;
            public string ChildGuid;
        }
    }
}
