using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Kit.Editor;
using Emilia.Node.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用创建节点菜单信息提供者
    /// </summary>
    public interface IUniversalCreateNodeMenuInfoProvider
    {
        /// <summary>
        /// 菜单标题
        /// </summary>
        /// <returns></returns>
        string GetTitle();

        /// <summary>
        /// 创建节点树
        /// </summary>
        void CreateNodeTree(CreateNodeContext createNodeContext, Action<CreateNodeMenuItem> groupCreate, Action<CreateNodeMenuItem> itemCreate);

        /// <summary>
        /// 创建节点
        /// </summary>
        bool CreateNode(CreateNodeInfo createNodeInfo, CreateNodeContext createNodeContext);
    }

    /// <summary>
    /// 通用创建节点菜单处理器
    /// </summary>
    [EditorHandle(typeof(EditorUniversalGraphAsset))]
    public class UniversalCreateNodeMenuHandle : CreateNodeMenuHandle, IUniversalCreateNodeMenuInfoProvider
    {
        private EditorGraphView editorGraphView;
        private Texture2D nullIcon;

        protected CreateNodeMenuProvider createNodeMenuProvider { get; private set; }

        public string GetTitle() => "Create Node";

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            this.editorGraphView = graphView;
            nullIcon = new Texture2D(1, 1);
            nullIcon.SetPixel(0, 0, Color.clear);
            nullIcon.Apply();

            createNodeMenuProvider = ScriptableObject.CreateInstance<CreateNodeMenuProvider>();
        }

        public override void InitializeCache(EditorGraphView graphView, List<ICreateNodeHandle> createNodeHandles)
        {
            InitializeRuntimeNodeCache(graphView, createNodeHandles);
            InitializeEditorNodeCache(graphView, createNodeHandles);
        }

        private void InitializeRuntimeNodeCache(EditorGraphView graphView, List<ICreateNodeHandle> createNodeHandles)
        {
            Type assetType = graphView.graphAsset.GetType();
            NodeToRuntimeAttribute attribute = assetType.GetCustomAttribute<NodeToRuntimeAttribute>(true);
            if (attribute == null) return;

            IList<Type> types = TypeCache.GetTypesDerivedFrom(attribute.baseRuntimeNodeType);
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface || type.IsGenericType) continue;

                CreateNodeHandleContext createNodeHandleContext = new();
                createNodeHandleContext.nodeType = type;
                createNodeHandleContext.defaultEditorNodeType = attribute.baseEditorNodeType;

                ICreateNodeHandle nodeHandle = EditorHandleUtility.CreateHandle<ICreateNodeHandle>(type);
                if (nodeHandle == null) continue;

                nodeHandle.Initialize(createNodeHandleContext);
                createNodeHandles.Add(nodeHandle);
            }
        }

        private void InitializeEditorNodeCache(EditorGraphView graphView, List<ICreateNodeHandle> createNodeHandles)
        {
            Type assetType = graphView.graphAsset.GetType();
            NodeToEditorAttribute attribute = assetType.GetCustomAttribute<NodeToEditorAttribute>(true);
            if (attribute == null) return;

            IList<Type> types = TypeCache.GetTypesDerivedFrom(attribute.baseEditorNodeType);
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface) continue;

                NodeMenuAttribute nodeMenuAttribute = type.GetCustomAttribute<NodeMenuAttribute>();
                if (nodeMenuAttribute == null) continue;

                CreateNodeHandle createNodeHandle = new();
                createNodeHandle.path = nodeMenuAttribute.path;
                createNodeHandle.priority = nodeMenuAttribute.priority;
                createNodeHandle.editorNodeType = type;

                createNodeHandles.Add(createNodeHandle);
            }
        }

        public override ICreateNodeCollector GetDefaultFilter(EditorGraphView graphView)
        {
            IEditorEdgeView edgeView = graphView.graphSelected.selected.OfType<IEditorEdgeView>().FirstOrDefault();
            if (edgeView != null) return new InsertNodeCollector(graphView, edgeView);

            return null;
        }

        public override void ShowCreateNodeMenu(EditorGraphView graphView, CreateNodeContext createNodeContext)
        {
            base.ShowCreateNodeMenu(graphView, createNodeContext);

            if (createNodeContext.nodeMenu == null) return;

            createNodeMenuProvider.Initialize(graphView, createNodeContext, this);
            SearchWindowContext searchWindowContext = new(createNodeContext.screenMousePosition);
            SearchWindow_Hook.Open<CreateNodeMenuProvider, SearchWindow_Hook>(searchWindowContext, createNodeMenuProvider);
        }

        /// <summary>
        /// 创建节点树
        /// </summary>
        public virtual void CreateNodeTree(CreateNodeContext createNodeContext, Action<CreateNodeMenuItem> groupCreate, Action<CreateNodeMenuItem> itemCreate)
        {
            // 用于存储分组路径及其对应的菜单项列表
            Dictionary<string, List<CreateNodeMenuItem>> groupItemsByPath = new();
            // 用于存储完整路径及其对应的节点菜单项
            Dictionary<string, CreateNodeMenuItem> nodeItemByFullPath = new();

            // 收集所有节点信息
            List<MenuNodeInfo> allNodeInfos = new();
            CollectAllCreateNodeInfos(this.editorGraphView, allNodeInfos, createNodeContext);

            // 根据收集器过滤或直接转换节点信息
            List<CreateNodeInfo> createNodeInfos = createNodeContext.nodeCollector != null
                ? createNodeContext.nodeCollector.Collect(allNodeInfos)
                : allNodeInfos.Select(info => new CreateNodeInfo(info)).ToList();

            // 构建节点菜单项并建立分组层级结构
            int createCount = createNodeInfos.Count;
            for (int i = 0; i < createCount; i++)
            {
                CreateNodeInfo createNodeInfo = createNodeInfos[i];

                string fullPath = createNodeInfo.menuInfo.path;
                // 构建分组层级并返回节点所在的层级
                int nodeLevel = BuildGroupHierarchy(fullPath, createNodeInfo);

                CreateNodeMenuItem nodeMenuItem = new();
                nodeMenuItem.info = createNodeInfo;
                nodeMenuItem.level = nodeLevel;

                nodeItemByFullPath[fullPath] = nodeMenuItem;
            }

            // 准备分组路径列表并按优先级排序
            List<string> groupPaths = new();
            groupPaths.AddRange(groupItemsByPath.Keys);

            // 按照分组中最高优先级排序
            groupPaths.Sort((a, b) => {
                int aMaxPriority = GetMaxPriority(groupItemsByPath[a]);
                int bMaxPriority = GetMaxPriority(groupItemsByPath[b]);
                return aMaxPriority.CompareTo(bMaxPriority);
            });

            // 准备节点路径列表并按优先级排序
            List<string> nodePaths = new();
            nodePaths.AddRange(nodeItemByFullPath.Keys);

            // 按照节点自身的优先级排序
            nodePaths.Sort((a, b) => {
                CreateNodeMenuItem aItem = nodeItemByFullPath[a];
                CreateNodeMenuItem bItem = nodeItemByFullPath[b];
                return aItem.info.menuInfo.priority.CompareTo(bItem.info.menuInfo.priority);
            });

            // 用于跟踪已经创建的节点路径,避免重复创建
            List<string> createdNodePaths = new();

            // 创建所有分组及其下的节点
            for (int i = 0; i < groupPaths.Count; i++)
            {
                string groupPath = groupPaths[i];
                CreateNodeMenuItem groupMenuItem = groupItemsByPath[groupPath].FirstOrDefault();
                groupCreate?.Invoke(groupMenuItem);

                // 在当前分组下添加所有属于该分组的节点
                for (int j = 0; j < nodePaths.Count; j++)
                {
                    string nodePath = nodePaths[j];
                    if (nodePath.Contains(groupPath) == false) continue;
                    AddItem(groupMenuItem, nodePath);
                }
            }

            // 创建未归入任何分组的节点(顶层节点)
            for (int i = 0; i < nodePaths.Count; i++)
            {
                string nodePath = nodePaths[i];
                if (createdNodePaths.Contains(nodePath)) continue;
                AddItem(null, nodePath);
            }

            // 构建分组层级结构
            int BuildGroupHierarchy(string path, CreateNodeInfo info)
            {
                string[] parts = path.Split('/');
                // 如果只有一层,说明没有分组
                if (parts.Length <= 1) return 0;

                string runningPath = string.Empty;
                int level = 0;

                int partAmount = parts.Length;
                // 遍历路径的每一部分(除了最后一个节点名称)
                for (int j = 0; j < partAmount - 1; j++)
                {
                    string title = parts[j];
                    // 累积构建当前的完整分组路径
                    runningPath = string.IsNullOrEmpty(runningPath) ? title : $"{runningPath}/{title}";

                    level = j + 1;

                    if (groupItemsByPath.ContainsKey(runningPath) == false) groupItemsByPath[runningPath] = new List<CreateNodeMenuItem>();

                    CreateNodeMenuItem menuItem = new();
                    menuItem.info = info;
                    menuItem.level = level;
                    menuItem.title = title;

                    groupItemsByPath[runningPath].Add(menuItem);
                }

                return level;
            }

            // 获取菜单项列表中的最高优先级
            int GetMaxPriority(List<CreateNodeMenuItem> items)
            {
                int maxPriority = int.MinValue;
                for (int i = 0; i < items.Count; i++)
                {
                    CreateNodeMenuItem item = items[i];
                    if (item.info.menuInfo.priority > maxPriority) maxPriority = item.info.menuInfo.priority;
                }
                return maxPriority;
            }

            // 添加节点菜单项
            void AddItem(CreateNodeMenuItem parent, string nodePath)
            {
                CreateNodeMenuItem menuItem = nodeItemByFullPath[nodePath];
                menuItem.parent = parent;

                // 设置节点图标,如果没有则使用透明图标
                Texture2D icon = nullIcon;
                if (menuItem.info.menuInfo.icon != null) icon = menuItem.info.menuInfo.icon;

                // 提取节点名称(路径的最后一部分)
                string nodeName = nodePath;
                string[] parts = nodePath.Split('/');
                if (parts.Length > 1) nodeName = parts[parts.Length - 1];

                // 创建最终的菜单项
                CreateNodeMenuItem itemMenu = new(menuItem.info, nodeName, menuItem.level + 1);
                itemMenu.info.menuInfo.icon = icon;

                itemCreate?.Invoke(itemMenu);

                // 标记该路径已创建,避免重复
                createdNodePaths.Add(nodePath);
            }
        }

        /// <summary>
        /// 收集所有创建节点信息
        /// </summary>
        protected virtual void CollectAllCreateNodeInfos(EditorGraphView graphView, List<MenuNodeInfo> createNodeInfos, CreateNodeContext createNodeContext)
        {
            int amount = graphView.createNodeMenu.createNodeHandleCacheList.Count;
            for (int i = 0; i < amount; i++)
            {
                ICreateNodeHandle nodeHandle = graphView.createNodeMenu.createNodeHandleCacheList[i];
                if (nodeHandle.validity == false) continue;

                MenuNodeInfo menuNodeInfo = new();
                menuNodeInfo.nodeData = nodeHandle.nodeData;
                menuNodeInfo.editorNodeAssetType = nodeHandle.editorNodeType;
                menuNodeInfo.path = nodeHandle.path;
                menuNodeInfo.priority = nodeHandle.priority;
                menuNodeInfo.icon = nodeHandle.icon;
                createNodeInfos.Add(menuNodeInfo);
            }
        }

        /// <summary>
        /// 创建节点
        /// </summary>
        public virtual bool CreateNode(CreateNodeInfo createNodeInfo, CreateNodeContext createNodeContext)
        {
            if (createNodeContext.nodeMenu == null) return false;
            EditorWindow window = this.editorGraphView.window;
            VisualElement windowRoot = window.rootVisualElement;
            Vector2 windowMousePosition = windowRoot.ChangeCoordinatesTo(windowRoot.parent, createNodeContext.screenMousePosition - window.position.position);
            Vector2 graphMousePosition = this.editorGraphView.contentViewContainer.WorldToLocal(windowMousePosition);

            Undo.IncrementCurrentGroup();

            IEditorNodeView nodeView = this.editorGraphView.nodeSystem.CreateNode(createNodeInfo.menuInfo.editorNodeAssetType, graphMousePosition, createNodeInfo.menuInfo.nodeData);
            createNodeInfo.postprocess?.Postprocess(this.editorGraphView, nodeView, createNodeContext);

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Undo.IncrementCurrentGroup();

            return true;
        }

        public override void Dispose(EditorGraphView graphView)
        {
            base.Dispose(graphView);
            this.editorGraphView = null;

            if (this.nullIcon != null)
            {
                Object.DestroyImmediate(nullIcon);
                nullIcon = null;
            }

            if (createNodeMenuProvider != null)
            {
                Object.DestroyImmediate(createNodeMenuProvider);
                createNodeMenuProvider = null;
            }
        }
    }
}