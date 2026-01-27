using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public class BoolToggleButtonAttributeDrawer : OdinAttributeDrawer<BoolToggleButtonAttribute>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            object weakSmartValue = Property.ValueEntry.WeakSmartValue;
            if (weakSmartValue is bool == false)
            {
                CallNextDrawer(label);
                return;
            }

            bool value = (bool) weakSmartValue;

            if (label != null) GUILayout.Label(label);

            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(20), GUILayout.ExpandWidth(true));
            Rect leftRect = rect.AlignLeft(rect.width / 2);
            Rect rightRect = rect.AlignRight(rect.width / 2);

            GUIContent leftContent = new GUIContent(Attribute.trueText);
            GUIContent rightContent = new GUIContent(Attribute.falseText);

            if (SirenixEditorGUI.SDFIconButton(leftRect, leftContent, SdfIconType.None, selected: value)) Property.ValueEntry.WeakSmartValue = true;
            if (SirenixEditorGUI.SDFIconButton(rightRect, rightContent, SdfIconType.None, selected: ! value)) Property.ValueEntry.WeakSmartValue = false;
        }
    }
}