using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Kit.Editor;
using Emilia.Reflection.Editor;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 编辑器GraphView
    /// </summary>
    public class EditorGraphView : GraphView_Hook
    {
        private static Dictionary<EditorGraphAsset, EditorGraphView> graphViews = new();

        /// <summary>
        /// 聚焦的GraphView
        /// </summary>
        public static EditorGraphView focusedGraphView { get; set; }

        private GraphHandle graphHandle;
        private List<GraphData> datas = new();

        private Dictionary<Type, BasicGraphViewModule> modules = new();
        private Dictionary<Type, CustomGraphViewModule> customModules = new();

        private List<IEditorNodeView> _nodeViews = new();
        private List<IEditorEdgeView> _edgeViews = new();
        private List<IEditorItemView> _itemViews = new();

        private GraphContentZoomer graphZoomer;
        private EditorCoroutine loadElementCoroutine;

        /// <summary>
        /// 逻辑位置
        /// </summary>
        public Vector3 logicPosition { get; set; }

        /// <summary>
        /// 逻辑缩放
        /// </summary>
        public Vector3 logicScale { get; set; }

        /// <summary>
        /// 所有IEditorNodeView
        /// </summary>
        public IReadOnlyList<IEditorNodeView> nodeViews => this._nodeViews;

        /// <summary>
        /// 所有IEditorEdgeView
        /// </summary>
        public IReadOnlyList<IEditorEdgeView> edgeViews => this._edgeViews;

        /// <summary>
        /// 所有IEditorItemView
        /// </summary>
        public IReadOnlyList<IEditorItemView> itemViews => this._itemViews;

        /// <summary>
        /// 加载的GraphAsset
        /// </summary>
        public EditorGraphAsset graphAsset { get; set; }

        /// <summary>
        /// Element缓存
        /// </summary>
        public GraphElementCache graphElementCache { get; set; }

        /// <summary>
        /// 本地设置
        /// </summary>
        public GraphLocalSettingSystem graphLocalSettingSystem { get; set; }

        /// <summary>
        /// 操作
        /// </summary>
        public GraphOperate graphOperate { get; set; }

        /// <summary>
        /// 拷贝粘贴处理
        /// </summary>
        public GraphCopyPaste graphCopyPaste { get; set; }

        /// <summary>
        /// 撤销处理
        /// </summary>
        public GraphUndo graphUndo { get; set; }

        /// <summary>
        /// 保存处理
        /// </summary>
        public GraphSave graphSave { get; set; }

        /// <summary>
        /// 选中处理
        /// </summary>
        public GraphSelected graphSelected { get; set; }

        /// <summary>
        /// 面板管理
        /// </summary>
        public GraphPanelSystem graphPanelSystem { get; set; }

        /// <summary>
        /// 快捷键管理
        /// </summary>
        public GraphHotKeys hotKeys { get; set; }

        /// <summary>
        /// 节点管理
        /// </summary>
        public GraphNodeSystem nodeSystem { get; set; }

        /// <summary>
        /// 连接管理
        /// </summary>
        public GraphConnectSystem connectSystem { get; set; }

        /// <summary>
        /// Item管理
        /// </summary>
        public GraphItemSystem itemSystem { get; set; }

        /// <summary>
        /// 操作菜单
        /// </summary>
        public GraphOperateMenu operateMenu { get; set; }

        /// <summary>
        /// 创建节点菜单
        /// </summary>
        public GraphCreateNodeMenu createNodeMenu { get; set; }

        /// <summary>
        /// 创建Item菜单
        /// </summary>
        public GraphCreateItemMenu createItemMenu { get; set; }

        /// <summary>
        /// 拖拽管理
        /// </summary>
        public GraphDragAndDrop dragAndDrop { get; set; }

        /// <summary>
        /// 每帧最大加载时间（毫秒）
        /// </summary>
        public float maxLoadTimeMs { get; set; } = 0.0416f;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public double lastUpdateTime { get; set; }

        /// <summary>
        /// 加载进度
        /// </summary>
        public float loadProgress { get; set; }

        /// <summary>
        /// 初始化完成
        /// </summary>
        public bool isInitialized { get; set; }

        /// <summary>
        /// 是否聚焦
        /// </summary>
        public bool isFocus { get; set; }

        /// <summary>
        /// 当前窗口
        /// </summary>
        public EditorWindow window { get; set; }

        /// <summary>
        /// 更新事件
        /// </summary>
        public event Action onUpdate;

        /// <summary>
        /// 逻辑Transform改变事件
        /// </summary>
        public event Action<Vector3, Vector3> onLogicTransformChange;

        /// <summary>
        /// GraphAsset改变事件
        /// </summary>
        public event Action<EditorGraphAsset> onGraphAssetChange;

        public void Initialize()
        {
            InitializeModule();

            graphLocalSettingSystem = GetModule<GraphLocalSettingSystem>();
            graphOperate = GetModule<GraphOperate>();
            graphCopyPaste = GetModule<GraphCopyPaste>();
            graphUndo = GetModule<GraphUndo>();
            graphSave = GetModule<GraphSave>();
            graphSelected = GetModule<GraphSelected>();
            graphPanelSystem = GetModule<GraphPanelSystem>();
            hotKeys = GetModule<GraphHotKeys>();
            nodeSystem = GetModule<GraphNodeSystem>();
            connectSystem = GetModule<GraphConnectSystem>();
            itemSystem = GetModule<GraphItemSystem>();
            operateMenu = GetModule<GraphOperateMenu>();
            createNodeMenu = GetModule<GraphCreateNodeMenu>();
            createItemMenu = GetModule<GraphCreateItemMenu>();
            dragAndDrop = GetModule<GraphDragAndDrop>();

            graphElementCache = new GraphElementCache();

            serializeGraphElements = OnSerializeGraphElements;
            canPasteSerializedData = OnCanPasteSerializedData;
            unserializeAndPaste = OnUnserializeAndPaste;
            viewTransformChanged = OnViewTransformChanged;
            graphViewChanged = OnGraphViewChanged;
            elementResized = OnElementResized;

            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseUpEvent>(OnMouseUp);

            RegisterCallback<MouseEnterEvent>((_) => OnEnterFocus());
            RegisterCallback<MouseMoveEvent>((_) => OnFocus());
            RegisterCallback<MouseLeaveEvent>((_) => OnExitFocus());

            SetupZoom(0.15f, 3f);
            SetViewTransform(Vector3.zero, Vector3.one, 0);
        }

        private void InitializeModule()
        {
            modules.Clear();

            IList<Type> types = TypeCache.GetTypesDerivedFrom<BasicGraphViewModule>();
            List<BasicGraphViewModule> moduleList = new();

            foreach (Type type in types)
            {
                if (type.IsAbstract) continue;
                BasicGraphViewModule module = ReflectUtility.CreateInstance(type) as BasicGraphViewModule;
                if (module == null) continue;
                moduleList.Add(module);
            }

            moduleList.Sort((x, y) => x.order.CompareTo(y.order));

            foreach (BasicGraphViewModule module in moduleList) modules.Add(module.GetType(), module);
        }

        /// <summary>
        /// 获取模块
        /// </summary>
        public T GetModule<T>() where T : GraphViewModule
        {
            T result = this.modules.GetValueOrDefault(typeof(T)) as T;
            if (result != null) return result;

            return this.customModules.GetValueOrDefault(typeof(T)) as T;
        }

        public void OnEnterFocus()
        {
            if (isInitialized == false) return;

            if (isFocus) return;
            isFocus = true;

            this.graphHandle?.OnEnterFocus(this);
        }

        public void OnFocus()
        {
            if (isInitialized == false) return;

            if (isFocus == false) OnEnterFocus();
            
            if (focusedGraphView != this)
            {
                focusedGraphView = this;
                graphUndo.OnUndoRedoPerformed(true);
            }
            
            this.graphHandle?.OnFocus(this);
        }

        public void OnExitFocus()
        {
            if (isInitialized == false) return;

            if (isFocus == false) return;
            isFocus = false;

            this.graphHandle?.OnExitFocus(this);
        }

        public void OnUpdate()
        {
            lastUpdateTime = EditorApplication.timeSinceStartup;
            this.graphHandle?.OnUpdate(this);
            onUpdate?.Invoke();
        }

        /// <summary>
        /// 重新加载
        /// </summary>
        public void Reload(EditorGraphAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("Reload asset 为空");
                return;
            }

            graphViews[asset] = this;

            onGraphAssetChange?.Invoke(asset);
            loadProgress = 0;
            isInitialized = false;

            if (loadElementCoroutine != null) EditorCoroutineUtility.StopCoroutine(loadElementCoroutine);
            loadElementCoroutine = null;

            bool allReload = graphAsset == null || graphAsset.GetType() != asset.GetType();
            graphAsset = asset;

            ReloadData();

            schedule.Execute(OnReload).ExecuteLater(1);

            void OnReload()
            {
                if (allReload) AllReload();
                else ElementReload();
            }
        }

        /// <summary>
        /// 简单加载
        /// </summary>
        public void SimpleReload(EditorGraphAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("SimpleLoad asset 为空");
                return;
            }

            graphAsset = asset;

            ReloadData();
            ReloadHandle();
            ReloadModule();
            RemoveAllElement();

            graphElementCache.BuildCache(this);
            loadProgress = 1;
            isInitialized = true;
        }

        private void AllReload()
        {
            ReloadHandle();
            ReloadModule();

            RemoveAllElement();
            loadElementCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(LoadElement());
        }

        private void ReloadHandle()
        {
            if (this.graphHandle != null) graphHandle.Dispose(this);
            this.graphHandle = EditorHandleUtility.CreateHandle<GraphHandle>(graphAsset.GetType());
            this.graphHandle?.Initialize(this);
        }

        private void ReloadData()
        {
            Type currentType = graphAsset.GetType();

            datas.Clear();

            while (currentType != null)
            {
                Type graphType = GraphDataCache.GetGraphDataType(currentType);
                if (graphType != null)
                {
                    GraphData graphData = ReflectUtility.CreateInstance(graphType) as GraphData;
                    if (graphData != null)
                    {
                        graphData.OnCreate(this);
                        this.datas.Add(graphData);
                    }
                }

                if (currentType == typeof(EditorGraphAsset)) break;
                currentType = currentType.BaseType;
            }
        }

        private void ReloadModule()
        {
            foreach (CustomGraphViewModule customModule in this.customModules.Values) customModule.Dispose();
            this.customModules.Clear();

            foreach (BasicGraphViewModule module in this.modules.Values) module.Dispose();

            foreach (BasicGraphViewModule module in this.modules.Values) module.Initialize(this);

            graphHandle?.InitializeCustomModule(this, customModules);

            foreach (CustomGraphViewModule customModule in this.customModules.Values) customModule.Initialize(this);

            foreach (BasicGraphViewModule module in this.modules.Values) module.AllModuleInitializeSuccess();
            foreach (CustomGraphViewModule customModule in this.customModules.Values) customModule.AllModuleInitializeSuccess();

            graphHandle?.AllModuleInitializeSuccess(this);
        }

        private void ElementReload()
        {
            RemoveAllElement();
            loadElementCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(LoadElement());
        }

        private IEnumerator LoadElement()
        {
            graphElementCache.BuildCache(this);

            this.graphHandle?.OnLoadBefore(this);

            yield return LoadNodeView();
            yield return LoadEdge();
            yield return LoadItem();

            LoadSuccess();
        }

        private IEnumerator LoadNodeView()
        {
            if (graphAsset == null) yield break;

            int amount = graphAsset.nodes.Count;
            for (int i = 0; i < amount; i++)
            {
                if (graphAsset == null) yield break;

                EditorNodeAsset node = graphAsset.nodes[i];
                AddNodeView(node);

                loadProgress = (i + 1) / (float) (graphAsset.nodes.Count + graphAsset.edges.Count + graphAsset.items.Count);
                if (EditorApplication.timeSinceStartup - lastUpdateTime > maxLoadTimeMs) yield return 0;
            }
        }

        private IEnumerator LoadEdge()
        {
            if (graphAsset == null) yield break;

            int amount = graphAsset.edges.Count;
            for (int i = 0; i < amount; i++)
            {
                if (graphAsset == null) yield break;

                EditorEdgeAsset edge = graphAsset.edges[i];
                AddEdgeView(edge);

                loadProgress = (graphAsset.nodes.Count + i + 1) / (float) (graphAsset.nodes.Count + graphAsset.edges.Count + graphAsset.items.Count);
                if (EditorApplication.timeSinceStartup - lastUpdateTime > maxLoadTimeMs) yield return 0;
            }
        }

        private IEnumerator LoadItem()
        {
            if (graphAsset == null) yield break;

            int amount = graphAsset.items.Count;
            for (int i = 0; i < amount; i++)
            {
                if (graphAsset == null) yield break;

                EditorItemAsset itemAsset = graphAsset.items[i];
                AddItemView(itemAsset);

                loadProgress = (graphAsset.nodes.Count + graphAsset.edges.Count + i + 1) / (float) (graphAsset.nodes.Count + graphAsset.edges.Count + graphAsset.items.Count);
                if (EditorApplication.timeSinceStartup - lastUpdateTime > maxLoadTimeMs) yield return 0;
            }
        }

        private void LoadSuccess()
        {
            if (graphAsset == null)
            {
                Debug.LogError("加载失败 graphAsset 为空");
                return;
            }

            loadElementCoroutine = null;
            loadProgress = 1;
            isInitialized = true;

            this.graphHandle?.OnLoadAfter(this);
        }

        /// <summary>
        /// 添加IEditorNodeView并添加到Asset中
        /// </summary>
        public IEditorNodeView AddNode(EditorNodeAsset nodeAsset)
        {
            graphAsset.AddNode(nodeAsset);
            IEditorNodeView nodeView = AddNodeView(nodeAsset);
            return nodeView;
        }

        /// <summary>
        /// 添加IEditorNodeView
        /// </summary>
        public IEditorNodeView AddNodeView(EditorNodeAsset nodeAsset)
        {
            Type nodeViewType = GraphTypeCache.GetNodeViewType(nodeAsset.GetType());
            if (nodeViewType == null)
            {
                Debug.LogError($"AddNodeView {nodeAsset.GetType()}找不到IEditorNodeView，请在找不到IEditorNodeView使用EditorNodeAttribute指定");
                return null;
            }

            IEditorNodeView nodeView = null;

            try
            {
                nodeView = ReflectUtility.CreateInstance(nodeViewType) as IEditorNodeView;
                nodeView.Initialize(this, nodeAsset);
            }
            catch (Exception e)
            {
                Debug.LogError($"AddNodeView {nodeViewType.FullName} 创建失败 {e}");
                return null;
            }

            AddElement(nodeView.element);

            this._nodeViews.Add(nodeView);
            graphElementCache.SetNodeViewCache(nodeAsset.id, nodeView);
            return nodeView;
        }

        /// <summary>
        /// 移除IEditorNodeView
        /// </summary>
        public void RemoveNodeView(IEditorNodeView nodeView)
        {
            if (nodeView == null)
            {
                Debug.LogError("RemoveNodeView nodeView 为空");
                return;
            }

            if (nodeView.asset != null) graphElementCache.RemoveNodeViewCache(nodeView.asset.id);

            try
            {
                nodeView.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"RemoveNodeView nodeView 异常 {e}");
            }

            if (nodeView.element != null) RemoveElement(nodeView.element);
            this._nodeViews.Remove(nodeView);
        }

        /// <summary>
        /// 添加IEditorEdgeView并添加到Asset中
        /// </summary>
        public IEditorEdgeView AddEdge(EditorEdgeAsset asset)
        {
            graphAsset.AddEdge(asset);
            IEditorEdgeView edgeView = AddEdgeView(asset);
            return edgeView;
        }

        /// <summary>
        /// 添加IEditorEdgeView
        /// </summary>
        public IEditorEdgeView AddEdgeView(EditorEdgeAsset asset)
        {
            IEditorNodeView inputNode = graphElementCache.GetEditorNodeView(asset.inputNodeId);
            IEditorNodeView outputNode = graphElementCache.GetEditorNodeView(asset.outputNodeId);

            if (inputNode == null || outputNode == null) return null;

            IEditorPortView inputPort = inputNode.GetPortView(asset.inputPortId);
            IEditorPortView outputPort = outputNode.GetPortView(asset.outputPortId);

            if (inputPort == null || outputPort == null) return null;

            Type edgeViewType = GraphTypeCache.GetEdgeViewType(asset.GetType());
            if (edgeViewType == null)
            {
                Debug.LogError($"AddEdgeView时 {asset.GetType()}找不到IEditorEdgeView，请在找不到IEditorEdgeView使用EditorEdgeAttribute指定");
                return null;
            }

            IEditorEdgeView edgeView;

            try
            {
                edgeView = ReflectUtility.CreateInstance(edgeViewType) as IEditorEdgeView;
                edgeView.Initialize(this, asset);
            }
            catch (Exception e)
            {
                Debug.LogError($"AddEdgeView {edgeViewType.FullName} 创建失败 {e}");
                return null;
            }

            AddElement(edgeView.edgeElement);

            this._edgeViews.Add(edgeView);
            graphElementCache.SetEdgeViewCache(asset.id, edgeView);
            return edgeView;
        }

        /// <summary>
        /// 移除IEditorEdgeView
        /// </summary>
        public void RemoveEdgeView(IEditorEdgeView edge)
        {
            if (edge == null)
            {
                Debug.LogError("RemoveEdgeView edge 为空");
                return;
            }

            if (edge.asset != null) graphElementCache.RemoveEdgeViewCache(edge.asset.id);

            try
            {
                edge.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"RemoveEdgeView edge 异常 {e}");
            }

            if (edge.edgeElement != null)
            {
                RemoveElement(edge.edgeElement);
                edge.inputPortView?.portElement.Disconnect(edge.edgeElement);
                edge.outputPortView?.portElement.Disconnect(edge.edgeElement);
            }

            this._edgeViews.Remove(edge);
        }

        /// <summary>
        /// 添加IEditorItemView并添加到Asset中
        /// </summary>
        public IEditorItemView AddItem(EditorItemAsset asset)
        {
            graphAsset.AddItem(asset);
            IEditorItemView itemView = AddItemView(asset);
            return itemView;
        }

        /// <summary>
        /// 添加IEditorItemView
        /// </summary>
        public IEditorItemView AddItemView(EditorItemAsset asset)
        {
            Type itemViewType = GraphTypeCache.GetItemViewType(asset.GetType());
            if (itemViewType == null)
            {
                Debug.LogError($"AddNodeView {asset.GetType()}找不到IEditorItemView，请在找不到IEditorItemView使用EditorItemAttribute指定");
                return null;
            }

            IEditorItemView itemView;
            try
            {
                itemView = ReflectUtility.CreateInstance(itemViewType) as IEditorItemView;
                itemView.Initialize(this, asset);
            }
            catch (Exception e)
            {
                Debug.LogError($"AddItemView {itemViewType.FullName} 创建失败 {e}");
                return null;
            }

            if (itemView.element == null)
            {
                Debug.LogError($"AddItemView {itemViewType.FullName} element 为空");
                return null;
            }

            AddElement(itemView.element);

            this._itemViews.Add(itemView);
            graphElementCache.SetItemViewCache(asset.id, itemView);
            return itemView;
        }

        /// <summary>
        /// 移除IEditorItemView
        /// </summary>
        public void RemoveItemView(IEditorItemView item)
        {
            if (item == null)
            {
                Debug.LogError("RemoveItemView item 为空");
                return;
            }
            if (item.asset != null) graphElementCache.RemoveItemViewCache(item.asset.id);

            try
            {
                item.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"RemoveItemView item 异常 {e}");
            }

            if (item.element != null) RemoveElement(item.element);
            this._itemViews.Remove(item);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            OperateMenuContext menuContext = new();
            menuContext.evt = evt;
            menuContext.graphView = this;

            operateMenu.BuildMenu(menuContext);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatiblePorts = new();

            IEditorPortView startPortView = startPort as IEditorPortView;
            if (startPortView == null) return compatiblePorts;

            foreach (Port port in this.ports)
            {
                IEditorPortView portView = port as IEditorPortView;
                if (portView == null)
                {
                    Debug.LogError("端口需要继承IEditorPortView");
                    continue;
                }

                if (startPortView.master == portView.master) continue;

                bool canConnect = connectSystem.CanConnect(startPortView, portView);
                if (canConnect) compatiblePorts.Add(port);
            }

            return compatiblePorts;
        }

        private string OnSerializeGraphElements(IEnumerable<GraphElement> elements) => graphCopyPaste.SerializeGraphElementsCallback(elements);

        private bool OnCanPasteSerializedData(string data) => graphCopyPaste.CanPasteSerializedDataCallback(data);

        private void OnUnserializeAndPaste(string operationName, string data)
        {
            GraphElement[] pasteObjects = graphCopyPaste.UnserializeAndPasteCallback(operationName, data).ToArray();

            SetSelection(pasteObjects.OfType<ISelectable>().ToList());
            UpdateSelected();

            clipboard_Internal = graphCopyPaste.SerializeGraphElementsCallback(pasteObjects);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (graphViewChange.elementsToRemove == null || graphViewChange.elementsToRemove.Count <= 0) return graphViewChange;

            Undo.IncrementCurrentGroup();

            Delete(graphViewChange.elementsToRemove);
            graphViewChange.elementsToRemove.Clear();
            UpdateSelected();

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Undo.IncrementCurrentGroup();

            return graphViewChange;
        }

        private void Delete(List<GraphElement> elementsToRemove)
        {
            int amount = elementsToRemove.Count;
            for (int i = 0; i < amount; i++)
            {
                GraphElement element = elementsToRemove[i];
                IDeleteGraphElement iDeleteGraphElement = element as IDeleteGraphElement;
                if (iDeleteGraphElement != null) iDeleteGraphElement.Delete();
            }
        }

        /// <summary>
        /// 更新逻辑Transform
        /// </summary>
        public void UpdateLogicTransform(Vector3 position, Vector3 scale)
        {
            logicPosition = position;
            logicScale = scale;

            onLogicTransformChange?.Invoke(logicPosition, logicScale);
        }

        private void OnViewTransformChanged(GraphView graphview)
        {
            UpdateLogicTransform(graphview.viewTransform.position, graphview.viewTransform.scale);
        }

        private void OnElementResized(VisualElement element)
        {
            IResizedGraphElement graphElement = element as IResizedGraphElement;
            if (graphElement != null) graphElement.OnElementResized();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            UpdateSelected();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            schedule.Execute(UpdateSelected).ExecuteLater(1);
        }

        private void OnUndoRedoPerformed()
        {
            graphUndo?.UndoRedoPerformed();
        }

        /// <summary>
        /// 更新选中
        /// </summary>
        public void UpdateSelected()
        {
            graphSelected?.UpdateSelected(selection.OfType<ISelectedHandle>().ToList());
        }

        /// <summary>
        /// 设置选中
        /// </summary>
        public void SetSelection(List<ISelectable> selectables)
        {
            ClearSelection();

            for (int i = 0; i < selectables.Count; i++)
            {
                ISelectable selectable = selectables[i];
                AddToSelection(selectable);
            }
        }

        /// <summary>
        /// 发送事件
        /// </summary>
        public void SendGraphEvent(IGraphEvent graphEvent)
        {
            graphEvent.graphView = this;
            this.SendEvent_Internal(graphEvent.eventTarget, DispatchMode_Internals.Immediate);
        }

        /// <summary>
        /// 发送事件
        /// </summary>
        public void SendEventImmediate(EventBase eventBase)
        {
            eventBase.target = this;
            this.SendEvent_Internal(eventBase, DispatchMode_Internals.Immediate);
        }

        /// <summary>
        /// 注册Undo
        /// </summary>
        public void RegisterCompleteObjectUndo(string name)
        {
            List<Object> objects = graphAsset.CollectAsset();
            Undo.RegisterCompleteObjectUndo(objects.ToArray(), name);
            graphSave.SetDirty();
        }

        /// <summary>
        /// 注册Undo
        /// </summary>
        public void RecordObjectUndo(string name)
        {
            List<Object> objects = graphAsset.CollectAsset();
            Undo.RecordObjects(objects.ToArray(), name);
            graphSave.SetDirty();
        }

        /// <summary>
        /// 获取GraphData
        /// </summary>
        public T GetGraphData<T>()
        {
            for (var i = 0; i < this.datas.Count; i++)
            {
                GraphData data = this.datas[i];
                if (data is T result) return result;
            }

            return default;
        }

        private void RemoveAllElement()
        {
            foreach (GraphElement graphElement in graphElements)
            {
                IRemoveViewElement removeViewElement = graphElement as IRemoveViewElement;
                removeViewElement?.RemoveView();
            }
        }

        protected override bool OverrideOnKeyDownShortcut(KeyDownEvent evt) => true;

        protected override bool OverrideUpdateContentZoomer()
        {
            if (minScale != maxScale)
            {
                if (graphZoomer == null)
                {
                    graphZoomer = new GraphContentZoomer();
                    this.AddManipulator(graphZoomer);
                }

                graphZoomer.minScale = minScale;
                graphZoomer.maxScale = maxScale;
                graphZoomer.scaleStep = scaleStep;
                graphZoomer.referenceScale = referenceScale;
            }
            else
            {
                if (graphZoomer != null) this.RemoveManipulator(graphZoomer);
            }

            ValidateTransform();
            return true;
        }

        private EditorCoroutine updateViewTransformCoroutine;

        /// <summary>
        /// 设置视图Transform
        /// </summary>
        public void SetViewTransform(Vector3 newPosition, Vector3 newScale, float animationTime = 0.2f)
        {
            // 延迟一帧执行，确保在正确的UI更新周期中执行
            schedule.Execute(OnSetViewTransform).ExecuteLater(1);

            void OnSetViewTransform()
            {
                // 验证输入参数是否有效（检查无穷大和NaN）
                float validateFloat = newPosition.x + newPosition.y + newPosition.z + newScale.x + newScale.y + newScale.z;
                if (float.IsInfinity(validateFloat) || float.IsNaN(validateFloat)) return;

                // 将位置对齐到像素网格，避免模糊渲染
                newPosition.x = GUIUtility_Internals.RoundToPixelGrid_Internals(newPosition.x);
                newPosition.y = GUIUtility_Internals.RoundToPixelGrid_Internals(newPosition.y);

                UpdateLogicTransform(newPosition, newScale);

                // 根据time参数决定使用动画过渡还是立即应用
                if (animationTime > 0)
                {
                    // 停止之前的Transform动画协程（如果存在）
                    if (updateViewTransformCoroutine != null) EditorCoroutineUtility.StopCoroutine(updateViewTransformCoroutine);
                    updateViewTransformCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(SetTransformAnimation());
                }
                else
                {
                    // 立即应用新的Transform
                    contentViewContainer.transform.position = newPosition;
                    contentViewContainer.transform.scale = newScale;

                    UpdatePersistedViewTransform_Internals();
                    if (viewTransformChanged != null) viewTransformChanged(this);
                }
            }

            // Transform动画协程，通过线性插值实现平滑过渡
            IEnumerator SetTransformAnimation()
            {
                // 记录动画起始状态
                Vector2 startPosition = contentViewContainer.transform.position;
                Vector3 startScale = contentViewContainer.transform.scale;

                double startTime = EditorApplication.timeSinceStartup;

                // 动画循环，直到达到指定时长
                while (EditorApplication.timeSinceStartup - startTime < animationTime)
                {
                    float t = (float) ((EditorApplication.timeSinceStartup - startTime) / animationTime);

                    Vector2 currentPosition = Vector2.Lerp(startPosition, newPosition, t);
                    Vector3 currentScale = Vector3.Lerp(startScale, newScale, t);

                    contentViewContainer.transform.position = currentPosition;
                    contentViewContainer.transform.scale = currentScale;

                    // 等待下一帧
                    yield return 0;
                }

                // 动画结束，确保最终值精确应用
                contentViewContainer.transform.position = newPosition;
                contentViewContainer.transform.scale = newScale;

                viewTransformChanged?.Invoke(this);
                UpdatePersistedViewTransform_Internals();

                updateViewTransformCoroutine = null;
            }
        }

        /// <summary>
        /// 保存
        /// </summary>
        public void Save(bool force = true)
        {
            graphSave?.Save(force);
        }

        /// <summary>
        /// 有效性
        /// </summary>
        public bool Validate() => panel != null;

        public void Dispose()
        {
            if (graphAsset != null) graphViews.Remove(graphAsset);

            Save(false);

            if (loadElementCoroutine != null) EditorCoroutineUtility.StopCoroutine(loadElementCoroutine);
            loadElementCoroutine = null;

            RemoveAllElement();

            foreach (CustomGraphViewModule customModule in this.customModules.Values) customModule.Dispose();
            this.customModules.Clear();

            foreach (BasicGraphViewModule module in this.modules.Values) module.Dispose();

            graphElementCache.Clear();

            Undo.undoRedoPerformed -= OnUndoRedoPerformed;

            if (focusedGraphView == this) focusedGraphView = null;
            if (this.graphHandle != null)
            {
                this.graphHandle.Dispose(this);
                this.graphHandle = null;
            }
        }

        /// <summary>
        /// 根据Asset获取View
        /// </summary>
        public static EditorGraphView GetGraphView(EditorGraphAsset asset)
        {
            if (asset == null) return null;
            EditorGraphView graphView = graphViews.GetValueOrDefault(asset);
            if (graphView == null) return null;

            bool validate = graphView.Validate();
            if (validate) return graphView;

            graphViews.Remove(asset);
            return null;
        }
    }
}