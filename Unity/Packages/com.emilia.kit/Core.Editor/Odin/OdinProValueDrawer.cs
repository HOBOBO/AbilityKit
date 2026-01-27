using System;
using Sirenix.OdinInspector.Editor;
using Object = UnityEngine.Object;

namespace Emilia.Kit.Editor
{
    public abstract class OdinProValueDrawer<T> : OdinValueDrawer<T>, IDisposable
    {
        private PropertyTreeContainer _drawerContainer = new PropertyTreeContainer();
        private UnityEditorContainer _editorContainer = new UnityEditorContainer();

        protected override void Initialize()
        {
            base.Initialize();
            ValueEntry.Property.Tree.OnUndoRedoPerformed -= OnUndoRedoPerformed;
            ValueEntry.Property.Tree.OnUndoRedoPerformed += OnUndoRedoPerformed;
        }

        protected void DrawTarget(object target, Action<PropertyTree, InspectorProperty> onCheck = null)
        {
            if (onCheck == null) onCheck = (_, __) => Property.MarkSerializationRootDirty();
            _drawerContainer.DrawTargetCheck(target, onCheck);
        }

        protected void DrawEditor(Object target)
        {
            _editorContainer.OnInspectorGUI(target);
        }

        protected virtual void OnUndoRedoPerformed()
        {
            _drawerContainer.Dispose();
            _editorContainer.Dispose();
        }

        public virtual void Dispose()
        {
            ValueEntry.Property.Tree.OnUndoRedoPerformed -= OnUndoRedoPerformed;
            _drawerContainer.Dispose();
        }
    }
}