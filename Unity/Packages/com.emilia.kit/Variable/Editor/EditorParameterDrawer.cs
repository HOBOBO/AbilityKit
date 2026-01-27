using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using SerializationUtility = Sirenix.Serialization.SerializationUtility;

namespace Emilia.Variables.Editor
{
    public class EditorParameterDrawer : OdinValueDrawer<EditorParameter>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            EditorParameter parameter = this.ValueEntry.SmartValue;

            GUILayout.BeginHorizontal();

            GUILayout.Box("D", GUI.skin.button, GUILayout.Width(50));
            Event evt = Event.current;

            Rect lastRect = GUILayoutUtility.GetLastRect();

            if (evt.type == EventType.MouseDrag && lastRect.Contains(evt.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(EditorParameter.DragAndDropKey, parameter);
                DragAndDrop.StartDrag("DragEditorParameter");
            }

            EditorParameter dragParameter = DragAndDrop.GetGenericData(EditorParameter.DragAndDropKey) as EditorParameter;
            bool canDrag = dragParameter != null && dragParameter != parameter;

            if (evt.type == EventType.DragUpdated && lastRect.Contains(evt.mousePosition) && canDrag) DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
            if (evt.type == EventType.DragPerform && lastRect.Contains(evt.mousePosition) && canDrag)
            {
                Property.RecordForUndo("DragEditorParameter");

                parameter.value = (Variable) SerializationUtility.CreateCopy(dragParameter.value);
                DragAndDrop.AcceptDrag();
                
                Property.MarkSerializationRootDirty();
            }

            CallNextDrawer(label);

            GUILayout.EndHorizontal();
        }
    }
}