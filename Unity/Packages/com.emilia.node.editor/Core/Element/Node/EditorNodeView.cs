using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Kit.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using NodeView = UnityEditor.Experimental.GraphView.Node;
using Object = UnityEngine.Object;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Node表现元素
    /// </summary>
    public abstract class EditorNodeView : NodeView, IEditorNodeView
    {
        protected List<IEditorPortView> _portViews = new();
        protected Dictionary<string, IEditorPortView> _portViewMap = new();

        protected Dictionary<string, EditorNodeInputPortEditInfo> inputEditInfos = new();
        protected Dictionary<string, VisualElement> inputEditElements = new();
        protected Dictionary<string, InspectorPropertyField> inputFields = new();

        public EditorNodeAsset asset { get; private set; }

        public EditorGraphView graphView { get; private set; }

        /// <summary>
        /// 底层容器
        /// </summary>
        public VisualElement bottomLayerContainer { get; protected set; }

        /// <summary>
        /// 顶层容器
        /// </summary>
        public VisualElement topLayerContainer { get; protected set; }

        /// <summary>
        /// 顶部节点容器
        /// </summary>
        public VisualElement nodeTopContainer { get; protected set; }

        /// <summary>
        /// 底部节点容器
        /// </summary>
        public VisualElement nodeBottomContainer { get; protected set; }

        /// <summary>
        /// 底部端口容器
        /// </summary>
        public VisualElement portBottomContainer { get; protected set; }

        /// <summary>
        /// 顶部端口容器
        /// </summary>
        public VisualElement portTopContainer { get; protected set; }

        /// <summary>
        /// 底部节点端口容器
        /// </summary>
        public VisualElement portNodeBottomContainer { get; protected set; }

        /// <summary>
        /// 顶部节点端口容器
        /// </summary>
        public VisualElement portNodeTopContainer { get; protected set; }

        /// <summary>
        /// Input端口编辑控件容器
        /// </summary>
        public VisualElement inputEditContainer { get; protected set; }

        /// <summary>
        /// 资源Inspector容器
        /// </summary>
        public VisualElement assetContainer { get; protected set; }

        /// <summary>
        /// 标题
        /// </summary>
        public Label titleLabel { get; protected set; }

        /// <summary>
        /// 主题颜色
        /// </summary>
        public virtual Color topicColor { get; protected set; } = Color.black;

        public virtual GraphElement element => this;
        public IReadOnlyList<IEditorPortView> portViews => this._portViews;

        public bool isSelected { get; protected set; }
        protected virtual bool editInNode => false;

        protected virtual bool canDelete => true;
        protected virtual bool canCollapsible => true;
        protected virtual string styleFilePath { get; } = string.Empty;

        public override bool expanded
        {
            get => asset?.isExpanded ?? false;
            set
            {
                if (capabilities.HasFlag(Capabilities.Collapsible) == false) return;
                if (asset == null) return;

                RegisterCompleteObjectUndo("Graph SetExpanded");

                base.expanded = value;
                asset.isExpanded = value;

                RebuildPortView();
            }
        }

        public virtual void Initialize(EditorGraphView graphView, EditorNodeAsset asset)
        {
            this.graphView = graphView;
            this.asset = asset;

            InitializeNodeView();
            RebuildPortView();
        }

        protected virtual void InitializeNodeView()
        {
            // 移除默认的contents容器，构建自定义UI结构
            VisualElement contents = this.Q("contents");
            contents.RemoveFromHierarchy();

            bottomLayerContainer = new VisualElement();
            bottomLayerContainer.name = "bottom-layer-container";
            bottomLayerContainer.pickingMode = PickingMode.Ignore;

            topLayerContainer = new VisualElement();
            topLayerContainer.name = "top-layer-container";
            topLayerContainer.pickingMode = PickingMode.Ignore;

            nodeTopContainer = new VisualElement();
            nodeTopContainer.name = "node-top-container";
            nodeTopContainer.pickingMode = PickingMode.Ignore;

            nodeBottomContainer = new VisualElement();
            nodeBottomContainer.name = "node-bottom-container";
            nodeBottomContainer.pickingMode = PickingMode.Ignore;

            portBottomContainer = new VisualElement();
            portBottomContainer.name = "port-bottom-container";
            portBottomContainer.pickingMode = PickingMode.Ignore;

            portTopContainer = new VisualElement();
            portTopContainer.name = "port-top-container";
            portTopContainer.pickingMode = PickingMode.Ignore;

            portNodeBottomContainer = new VisualElement();
            portNodeBottomContainer.name = "port-node-bottom-container";
            portNodeBottomContainer.pickingMode = PickingMode.Ignore;

            portNodeTopContainer = new VisualElement();
            portNodeTopContainer.name = "port-node-top-container";
            portNodeTopContainer.pickingMode = PickingMode.Ignore;

            inputEditContainer = new VisualElement();
            inputEditContainer.name = "input-edit-container";
            inputEditContainer.pickingMode = PickingMode.Ignore;

            VisualElement layerCenter = mainContainer;

            int index = layerCenter.parent.IndexOf(layerCenter);
            layerCenter.parent.Insert(index, bottomLayerContainer);

            index = layerCenter.parent.IndexOf(layerCenter);
            layerCenter.parent.Insert(index + 1, topLayerContainer);

            VisualElement nodeCenter = titleContainer;

            index = nodeCenter.parent.IndexOf(nodeCenter);
            nodeCenter.parent.Insert(index, nodeTopContainer);

            index = nodeCenter.parent.IndexOf(nodeCenter);
            nodeCenter.parent.Insert(index + 1, nodeBottomContainer);

            bottomLayerContainer.Add(portBottomContainer);
            topLayerContainer.Add(portTopContainer);

            nodeTopContainer.Add(portNodeTopContainer);
            nodeBottomContainer.Add(portNodeBottomContainer);

            bottomLayerContainer.Insert(0, inputEditContainer);

            titleLabel = this.Q<Label>("title-label");

            if (string.IsNullOrEmpty(styleFilePath) == false)
            {
                StyleSheet styleSheet = ResourceUtility.LoadResource<StyleSheet>(styleFilePath);
                styleSheets.Add(styleSheet);
            }

            if (canDelete == false) capabilities &= ~Capabilities.Deletable;
            if (canCollapsible) capabilities |= Capabilities.Collapsible;

            SetPositionNoUndo(asset.position);
            SetColor(topicColor);
            SetExpandedNoUndo(asset.isExpanded);
        }

        protected virtual void RebuildPortView()
        {
            // 清理旧数据
            inputEditInfos.Clear();
            RemovePortViews();

            // 收集所有静态端口资产
            List<EditorPortInfo> portInfos = CollectStaticPortAssets();
            portInfos.Sort((a, b) => a.order.CompareTo(b.order));

            // key: (端口方向, 端口朝向), value: 该类别下的端口列表
            Dictionary<(EditorPortDirection, EditorOrientation), List<EditorPortInfo>> categorizedPorts = new();

            for (var i = 0; i < portInfos.Count; i++)
            {
                EditorPortInfo info = portInfos[i];

                bool connected = graphView.graphAsset.GetEdges(asset.id, info.id).Any();
                if (connected == false && expanded) continue;

                // 构建分类key：端口方向和朝向的组合
                var key = (info.direction, info.orientation);

                if (categorizedPorts.TryGetValue(key, out List<EditorPortInfo> portInfosInCategory) == false)
                {
                    portInfosInCategory = new List<EditorPortInfo>();
                    categorizedPorts[key] = portInfosInCategory;
                }

                // 将端口信息添加到对应的分类列表中
                categorizedPorts[key].Add(info);
            }

            // 遍历所有分类，为每个分类添加端口视图
            foreach (var category in categorizedPorts) AddPortViews(category.Value);

            // 刷新状态
            SetEditInNodeDisplay(editInNode && expanded == false);
            RebuildExpandPort();
        }

        private void RemovePortViews()
        {
            int removeCount = _portViews.Count;
            for (int i = removeCount - 1; i >= 0; i--)
            {
                IEditorPortView portView = _portViews[i];
                RemovePortView(portView);
            }
        }

        private void AddPortViews(List<EditorPortInfo> portInfos)
        {
            for (int i = 0; i < portInfos.Count; i++)
            {
                EditorPortInfo info = portInfos[i];
                AddPortView(i, info);
            }
        }

        /// <summary>
        /// 收集静态Port信息
        /// </summary>
        /// <returns></returns>
        public abstract List<EditorPortInfo> CollectStaticPortAssets();

        /// <summary>
        /// 根据Id获取PortView
        /// </summary>
        public virtual IEditorPortView GetPortView(string portId) => this._portViewMap.GetValueOrDefault(portId);

        /// <summary>
        /// 添加PortView
        /// </summary>
        public virtual IEditorPortView AddPortView(int index, EditorPortInfo info)
        {
            IEditorPortView portView = ReflectUtility.CreateInstance(info.nodePortViewType) as IEditorPortView;
            portView.Initialize(this, info);
            portView.onConnected += OnPortConnected;
            portView.OnDisconnected += OnPortDisconnected;

            this._portViews.Add(portView);
            this._portViewMap[info.id] = portView;

            return portView;
        }

        /// <summary>
        /// 移除PortView
        /// </summary>
        public virtual void RemovePortView(IEditorPortView portView)
        {
            if (portView == null) return;

            portView.onConnected -= OnPortConnected;
            portView.OnDisconnected -= OnPortDisconnected;

            if (inputEditElements.TryGetValue(portView.info.id, out VisualElement editElement)) editElement.RemoveFromHierarchy();
            if (inputFields.TryGetValue(portView.info.id, out InspectorPropertyField field)) field.RemoveFromHierarchy();

            inputEditInfos.Remove(portView.info.id);
            inputEditElements.Remove(portView.info.id);
            inputFields.Remove(portView.info.id);

            this._portViews.Remove(portView);
            this._portViewMap.Remove(portView.info.id);

            portView.RemoveView();
        }

        protected virtual void OnPortConnected(IEditorPortView editorPortView, IEditorEdgeView editorEdgeView)
        {
            if (editorPortView.portDirection != EditorPortDirection.Input) return;
            if (editorPortView.edges.Count == 0) return;

            if (inputEditElements.TryGetValue(editorPortView.info.id, out VisualElement editElement)) editElement.AddToClassList("empty");
            if (inputFields.TryGetValue(editorPortView.info.id, out InspectorPropertyField field)) field.style.display = DisplayStyle.None;
        }

        protected virtual void OnPortDisconnected(IEditorPortView editorPortView, IEditorEdgeView editorEdgeView)
        {
            if (editorPortView.portDirection != EditorPortDirection.Input) return;
            if (editorPortView.edges.Count > 0) return;

            if (inputEditElements.TryGetValue(editorPortView.info.id, out VisualElement editElement)) editElement.RemoveFromClassList("empty");
            if (inputFields.TryGetValue(editorPortView.info.id, out InspectorPropertyField field)) field.style.display = DisplayStyle.Flex;
        }

        protected void RebuildExpandPort()
        {
            int amount = portViews.Count;
            for (int i = 0; i < amount; i++)
            {
                IEditorPortView portView = portViews[i];
                if (portView.portDirection != EditorPortDirection.Input) continue;

                if (inputEditInfos.TryGetValue(portView.info.id, out EditorNodeInputPortEditInfo info))
                {
                    AddInputEditContainer(info.portId, info.fieldPath, info.forceImGUIDraw);
                    continue;
                }

                AddEmptyInputEditContainer(portView.info.id);
            }

            if (inputEditElements.Count > 0)
            {
                EditorKit.UnityInvoke(SyncTop);

                void SyncTop()
                {
                    var pair = inputEditElements.FirstOrDefault();
                    IEditorPortView portView = portViews.FirstOrDefault(p => p.info.id == pair.Key);
                    if (portView == null) return;
                    float top = GetPortTop(portView);
                    top -= inputEditContainer.parent.layout.y;
                    inputEditContainer.style.top = top;
                }
            }
        }

        private float GetPortTop(IEditorPortView portView)
        {
            float top = 0;

            VisualElement visualElement = portView.portElement;

            while (visualElement != this && visualElement != null)
            {
                top += visualElement.layout.y;
                visualElement = visualElement.parent;
            }

            return top;
        }

        protected void AddInputEditContainer(string portId, string fieldPath, bool forceImGUIDraw = false)
        {
            VisualElement editContainer = new();
            editContainer.name = "edit-container";

            InspectorProperty inspectorProperty = asset.propertyTree.GetPropertyAtPath(fieldPath);
            InspectorPropertyField inspectorPropertyField = new(inspectorProperty, forceImGUIDraw, false);
            inspectorPropertyField.AddToClassList("port-input-element");
            editContainer.Add(inspectorPropertyField);

            editContainer.RegisterCallback<GeometryChangedEvent>(_ => {
                IEditorPortView portView = this._portViewMap.GetValueOrDefault(portId);
                if (portView == null) return;
                float editHeight = editContainer.resolvedStyle.height + editContainer.resolvedStyle.marginTop + editContainer.resolvedStyle.marginBottom;
                float portHeight = portView.portElement.layout.height;

                float portMargin = Math.Max(editHeight - portHeight, 0);
                portView.portElement.style.marginBottom = portMargin;
            });

            inputEditContainer.Add(editContainer);

            inputEditElements[portId] = editContainer;
            inputFields[portId] = inspectorPropertyField;
        }

        protected void AddEmptyInputEditContainer(string portId)
        {
            VisualElement editContainer = new();
            editContainer.name = "edit-container";
            editContainer.AddToClassList("empty");

            inputEditContainer.Add(editContainer);
            inputEditElements[portId] = editContainer;
        }

        public virtual void OnValueChanged(bool isSilent = false)
        {
            SetPositionNoUndo(asset.position);
            SetExpandedNoUndo(asset.isExpanded, true);
            foreach (InspectorPropertyField value in inputFields.Values) value.Update();

            if (isSilent == false) graphView.graphSave.SetDirty();
        }

        public void RegisterCompleteObjectUndo(string name)
        {
            Undo.RegisterCompleteObjectUndo(asset, name);
            graphView.graphSave.SetDirty();
        }

        public override void CollectElements(HashSet<GraphElement> collectedElementSet, Func<GraphElement, bool> conditionFunc)
        {
            int amount = _portViews.Count;
            for (int i = 0; i < amount; i++)
            {
                IEditorPortView portView = this._portViews[i];
                List<IEditorEdgeView> edges = portView.GetEdges();
                int edgeAmount = edges.Count;
                for (int j = 0; j < edgeAmount; j++)
                {
                    IEditorEdgeView edge = edges[j];
                    collectedElementSet.Add(edge.edgeElement);
                }
            }
        }

        /// <summary>
        /// 设置位置
        /// </summary>
        public override void SetPosition(Rect newPos)
        {
            if (capabilities.HasFlag(Capabilities.Movable) == false) return;
            RegisterCompleteObjectUndo("Graph MoveNode");
            base.SetPosition(newPos);
            asset.position = newPos;
        }

        /// <summary>
        /// 设置位置（无撤销）
        /// </summary>
        public void SetPositionNoUndo(Rect newPos)
        {
            if (capabilities.HasFlag(Capabilities.Movable) == false) return;
            base.SetPosition(newPos);
            asset.position = newPos;
        }

        /// <summary>
        /// 设置展开（无撤销）
        /// </summary>
        public void SetExpandedNoUndo(bool expanded, bool isRebuild = false)
        {
            if (capabilities.HasFlag(Capabilities.Collapsible) == false) return;

            bool isChanged = expanded != base.expanded;

            base.expanded = expanded;
            asset.isExpanded = expanded;

            if (isRebuild && isChanged) RebuildPortView();
        }

        /// <summary>
        /// 设置资源Inspector的显示状态
        /// </summary>
        public virtual void SetEditInNodeDisplay(bool display)
        {
            if (assetContainer != null)
            {
                assetContainer.RemoveFromHierarchy();
                assetContainer = null;
            }

            if (display == false) return;

            assetContainer = new IMGUIContainer(() => asset.propertyTree?.Draw());
            topLayerContainer.Add(assetContainer);
        }

        /// <summary>
        /// 设置节点主题颜色
        /// </summary>
        public virtual void SetColor(Color color)
        {
            topicColor = color;

            Color titleContainerColor = topicColor;
            titleContainerColor.a = 0.25f;
            titleContainer.style.backgroundColor = titleContainerColor;
        }

        /// <summary>
        /// 设置节点Tips
        /// </summary>
        public void SetTooltip(string tooltip)
        {
            asset.tips = tooltip;
            titleLabel.tooltip = tooltip;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) { }

        public virtual void Delete()
        {
            if (capabilities.HasFlag(Capabilities.Deletable) == false) return;
            graphView.nodeSystem.DeleteNode(this);
        }

        public virtual void RemoveView()
        {
            graphView.RemoveNodeView(this);
        }

        public virtual ICopyPastePack GetPack() => new NodeCopyPastePack(asset);

        public virtual bool Validate() => true;

        public virtual bool IsSelected() => isSelected;

        public virtual void Select()
        {
            isSelected = true;
        }

        public virtual void Unselect()
        {
            isSelected = false;
        }

        public virtual IEnumerable<Object> GetSelectedObjects()
        {
            if (editInNode && expanded == false) yield break;
            if (asset != null) yield return asset;
        }

        public override string ToString() => title;

        public virtual void Dispose()
        {
            int amount = this._portViews.Count;
            for (int i = 0; i < amount; i++)
            {
                IEditorPortView portView = this._portViews[i];
                portView.Dispose();
            }

            RemoveFromHierarchy();
        }
    }
}