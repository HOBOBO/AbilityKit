using System;
using Sirenix.OdinInspector.Editor;
using Object = UnityEngine.Object;

namespace Emilia.Kit.Editor
{
    public abstract class OdinProAttributeDrawer<T> : OdinAttributeDrawer<T>, IDisposable where T : Attribute
    {
        private PropertyTreeContainer _drawerContainer = new PropertyTreeContainer();
        private UnityEditorContainer _editorContainer = new UnityEditorContainer();

        protected void DrawTarget(object target, Action<PropertyTree, InspectorProperty> onCheck = null)
        {
            if (onCheck == null) onCheck = (_, __) => Property.MarkSerializationRootDirty();
            _drawerContainer.DrawTargetCheck(target, onCheck);
        }

        protected void DrawEditor(Object target)
        {
            _editorContainer.OnInspectorGUI(target);
        }

        public virtual void Dispose()
        {
            _drawerContainer.Dispose();
            _editorContainer.Dispose();
        }
    }
}