using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    [Serializable]
    public class PropertyTreeContainer
    {
        [Serializable]
        private class Drawer : SerializedScriptableObject
        {
            [NonSerialized, OdinSerialize, HideReferenceObjectPicker, HideLabel]
            public object _target;
        }

        private Dictionary<object, PropertyTree> _propertyTrees = new Dictionary<object, PropertyTree>();

        public void DrawTargetCheck(object target, Action<PropertyTree, InspectorProperty> onCheck = null)
        {
            if (target == null) return;
            PropertyTree propertyTree = GetPropertyTree(target);
            propertyTree.BeginDraw(false);
            foreach (InspectorProperty property in propertyTree.EnumerateTree(false, true))
            {
                EditorGUI.BeginChangeCheck();
                property.Draw();
                if (EditorGUI.EndChangeCheck()) onCheck?.Invoke(propertyTree, property);
            }

            propertyTree.EndDraw();
        }

        PropertyTree GetPropertyTree(object target)
        {
            if (_propertyTrees == null) _propertyTrees = new Dictionary<object, PropertyTree>();
            if (this._propertyTrees.TryGetValue(target, out PropertyTree propertyTree))
            {
                if (target != propertyTree.RootProperty.ValueEntry.WeakSmartValue) propertyTree = ResetPropertyTree(target);
                return propertyTree;
            }
            Drawer drawer = ScriptableObject.CreateInstance<Drawer>();
            drawer._target = target;
            propertyTree = PropertyTree.Create(drawer);
            this._propertyTrees.Add(target, propertyTree);
            return propertyTree;
        }

        public PropertyTree ResetPropertyTree(object target)
        {
            if (_propertyTrees == null) return null;
            if (_propertyTrees.TryGetValue(target, out PropertyTree propertyTree)) propertyTree.Dispose();
            PropertyTree newPropertyTree = PropertyTree.Create(target);
            _propertyTrees[target] = newPropertyTree;
            return newPropertyTree;
        }

        public void DisposeTarget(object target)
        {
            if (_propertyTrees == null) return;
            if (_propertyTrees.TryGetValue(target, out PropertyTree propertyTree)) propertyTree.Dispose();
            _propertyTrees.Remove(target);
        }

        public void Dispose()
        {
            if (this._propertyTrees == null) return;
            foreach (var propertyTree in _propertyTrees.Values) propertyTree.Dispose();
            _propertyTrees.Clear();
        }
    }
}