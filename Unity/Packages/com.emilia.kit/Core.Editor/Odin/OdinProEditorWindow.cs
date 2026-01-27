using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Kit.Editor
{
    [ShowOdinSerializedPropertiesInInspector]
    public class OdinProEditorWindow : EditorWindow, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private SerializationData serializationData;

        [NonSerialized, OdinSerialize]
        private PropertyTreeContainer _drawerContainer = new PropertyTreeContainer();

        [NonSerialized, OdinSerialize]
        private UnityEditorContainer _editorContainer = new UnityEditorContainer();

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            UnitySerializationUtility.SerializeUnityObject((Object) this, ref this.serializationData);
            OnBeforeSerialize();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            UnitySerializationUtility.DeserializeUnityObject((Object) this, ref this.serializationData);
            OnAfterDeserialize();
        }

        protected void DrawTarget(object target, Action<PropertyTree, InspectorProperty> onCheck = null)
        {
            _drawerContainer.DrawTargetCheck(target, onCheck);
        }

        protected void DrawEditor(Object target)
        {
            _editorContainer.OnInspectorGUI(target);
        }

        protected virtual void OnDestroy()
        {
            _drawerContainer.Dispose();
        }

        protected virtual void OnBeforeSerialize() { }

        protected virtual void OnAfterDeserialize() { }
    }
}