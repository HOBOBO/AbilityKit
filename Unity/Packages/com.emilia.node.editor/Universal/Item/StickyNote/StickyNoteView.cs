using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 便利贴节点表现元素
    /// </summary>
    [EditorItem(typeof(StickyNoteAsset))]
    public class StickyNoteView : StickyNote, IEditorItemView
    {
        protected StickyNoteAsset stickyAsset;
        public EditorItemAsset asset => stickyAsset;
        public GraphElement element => this;
        public EditorGraphView graphView { get; protected set; }
        public bool isSelected { get; protected set; }

        public void Initialize(EditorGraphView graphView, EditorItemAsset asset)
        {
            this.graphView = graphView;
            stickyAsset = asset as StickyNoteAsset;

            title = stickyAsset.stickyTitle;
            contents = stickyAsset.content;
            theme = stickyAsset.theme;
            fontSize = this.stickyAsset.fontSize;
            SetPositionNoUndo(stickyAsset.position);

            RegisterCallback<StickyNoteChangeEvent>((_) => {
                stickyAsset.stickyTitle = title;
                stickyAsset.content = contents;
                stickyAsset.fontSize = fontSize;
                stickyAsset.theme = theme;

                Rect position = GetPosition();
                stickyAsset.position = new Rect(position.x, position.y, style.width.value.value, style.height.value.value);

                this.graphView.RegisterCompleteObjectUndo("Graph StickyNoteChange");
            });

            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        protected virtual void OnMouseDown(MouseDownEvent evt)
        {
            graphView?.UpdateSelected();
        }

        public virtual void OnValueChanged(bool isSilent = false)
        {
            title = this.stickyAsset.stickyTitle;
            contents = this.stickyAsset.content;
            fontSize = this.stickyAsset.fontSize;
            theme = this.stickyAsset.theme;

            SetPositionNoUndo(stickyAsset.position);

            if (isSilent == false) graphView.graphSave.SetDirty();
        }

        public override void SetPosition(Rect rect)
        {
            base.SetPosition(rect);
            if (this.graphView == null) return;
            this.graphView.RegisterCompleteObjectUndo("Graph MoveNode");
            stickyAsset.position = rect;
        }

        public void SetPositionNoUndo(Rect newPos)
        {
            base.SetPosition(newPos);
            asset.position = newPos;
        }

        public ICopyPastePack GetPack() => new ItemCopyPastePack(asset);

        public void Delete()
        {
            this.graphView.itemSystem.DeleteItem(this);
        }

        public void RemoveView()
        {
            graphView.RemoveItemView(this);
        }

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
            yield return asset;
        }

        public void Dispose()
        {
            graphView = null;
        }
    }
}