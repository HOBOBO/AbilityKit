using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单系统
    /// </summary>
    public class GraphOperateMenu : BasicGraphViewModule
    {
        public const int SeparatorAt = 1200;

        private OperateMenuHandle handle;

        /// <summary>
        /// 缓存操作菜单信息
        /// </summary>
        public List<OperateMenuActionInfo> actionInfoCache { get; private set; } = new();

        public override int order => 1100;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            actionInfoCache.Clear();
            handle = EditorHandleUtility.CreateHandle<OperateMenuHandle>(this.graphView.graphAsset.GetType());
        }

        public override void AllModuleInitializeSuccess()
        {
            base.AllModuleInitializeSuccess();
            handle?.InitializeCache(this.graphView, actionInfoCache);
        }

        /// <summary>
        /// 构建菜单
        /// </summary>
        public void BuildMenu(OperateMenuContext menuContext)
        {
            if (this.graphView == null || this.graphView.isInitialized == false) return;

            if (handle == null)
            {
                Debug.LogError("未找到操作菜单处理器，请创建并继承OperateMenuHandle<>");
                return;
            }

            if (menuContext.graphView == null || menuContext.evt == null)
            {
                Debug.LogError("菜单上下文参数错误");
                return;
            }

            // 收集菜单项
            List<OperateMenuItem> graphMenuItems = new();
            handle.CollectMenuItems(this.graphView, graphMenuItems, menuContext);

            // 对菜单项进行分组和排序：先按类别分组，类别内按优先级排序
            var sortedItems = graphMenuItems
                .GroupBy(x => string.IsNullOrEmpty(x.category) ? x.menuName : x.category)
                .OrderBy(x => x.Min(y => y.priority))
                .SelectMany(x => x.OrderBy(z => z.priority));

            int lastPriority = int.MinValue;
            string lastCategory = string.Empty;

            foreach (OperateMenuItem item in sortedItems)
            {
                if (item.state == OperateMenuActionValidity.NotApplicable) continue;

                int priority = item.priority;
                // 根据优先级插入分隔符（当优先级跨越SeparatorAt的倍数时）
                if (lastPriority != int.MinValue && priority / SeparatorAt > lastPriority / SeparatorAt)
                {
                    string path = string.Empty;
                    if (lastCategory == item.category) path = item.category;
                    menuContext.evt.menu.AppendSeparator(path);
                }

                lastPriority = priority;
                lastCategory = item.category;

                string entryName = item.category + item.menuName;

                // 设置菜单项状态
                DropdownMenuAction.Status status = DropdownMenuAction.Status.Normal;
                if (item.state == OperateMenuActionValidity.Invalid) status = DropdownMenuAction.Status.Disabled;
                if (item.isOn) status |= DropdownMenuAction.Status.Checked;

                menuContext.evt.menu.AppendAction(entryName, _ => item.onAction?.Invoke(), status);
            }
        }

        public override void Dispose()
        {
            if (this.graphView == null) return;

            actionInfoCache.Clear();
            this.handle = null;
            base.Dispose();
        }
    }
}