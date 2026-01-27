using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 选择系统
    /// </summary>
    public class GraphSelected : BasicGraphViewModule
    {
        private GraphSelectedHandle handle;

        private List<ISelectedHandle> _selected = new();
        private List<IGraphSelectedDrawer> selectedDrawers = new();

        public override int order => 600;

        /// <summary>
        /// 当前选中的元素
        /// </summary>
        public IReadOnlyList<ISelectedHandle> selected => this._selected;

        /// <summary>
        /// 选中改变事件
        /// </summary>
        public event Action<IReadOnlyList<ISelectedHandle>> onSelectedChanged;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            this.handle = EditorHandleUtility.CreateHandle<GraphSelectedHandle>(graphView.graphAsset.GetType());
            handle?.Initialize(graphView);
        }

        public override void AllModuleInitializeSuccess()
        {
            base.AllModuleInitializeSuccess();

            int oldAmount = selectedDrawers.Count;
            for (int i = 0; i < oldAmount; i++)
            {
                IGraphSelectedDrawer drawer = selectedDrawers[i];
                drawer.Dispose();
            }

            selectedDrawers.Clear();

            handle?.CollectSelectedDrawer(this.graphView, this.selectedDrawers);

            int newAmount = selectedDrawers.Count;
            for (int i = 0; i < newAmount; i++)
            {
                IGraphSelectedDrawer drawer = selectedDrawers[i];
                drawer.Initialize(graphView);
            }
        }

        /// <summary>
        /// 更新选中
        /// </summary>
        public void UpdateSelected(List<ISelectedHandle> selection)
        {
            handle?.BeforeUpdateSelected(this.graphView, this._selected);

            UnSelected(this._selected);

            this._selected.Clear();
            if (selection != null) this._selected.AddRange(selection);

            handle?.UpdateSelectedInspector(this.graphView, _selected);
            UpdateSelectedDrawer(_selected);

            Selected(this._selected);

            handle?.AfterUpdateSelected(this.graphView, _selected);

            onSelectedChanged?.Invoke(_selected);
        }

        private void Selected(List<ISelectedHandle> selectables)
        {
            int amount = selectables.Count;
            for (int i = 0; i < amount; i++)
            {
                ISelectedHandle selectable = selectables[i];
                if (selectable == null) continue;
                selectable.Select();
            }
        }

        private void UnSelected(List<ISelectedHandle> selection)
        {
            int amount = selection.Count;
            for (int i = 0; i < amount; i++)
            {
                ISelectedHandle selectable = selection[i];
                if (selectable == null) continue;
                selectable.Unselect();
            }
        }

        /// <summary>
        /// 更新选中状态
        /// </summary>
        public void UpdateSelected()
        {
            UpdateSelected(this._selected.ToList());
        }

        /// <summary>
        /// 更新选中的绘制
        /// </summary>
        private void UpdateSelectedDrawer(List<ISelectedHandle> selection)
        {
            int amount = this.selectedDrawers.Count;
            for (int i = 0; i < amount; i++)
            {
                IGraphSelectedDrawer drawer = this.selectedDrawers[i];
                drawer.SelectedUpdate(selection);
            }
        }

        public override void Dispose()
        {
            int amount = selectedDrawers.Count;
            for (int i = 0; i < amount; i++)
            {
                IGraphSelectedDrawer drawer = selectedDrawers[i];
                drawer.Dispose();
            }

            if (this.handle != null)
            {
                handle.Dispose(this.graphView);
                handle = null;
            }

            base.Dispose();
        }
    }
}