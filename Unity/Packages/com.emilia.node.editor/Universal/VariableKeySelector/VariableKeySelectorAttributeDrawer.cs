using Emilia.Kit;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;
using Emilia.Variables.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 变量Key选择器绘制
    /// </summary>
    public class VariableKeySelectorAttributeDrawer : OdinAttributeDrawer<VariableKeySelectorAttribute>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (Property.ValueEntry.TypeOfValue != typeof(string)) return;

            string key = Property.ValueEntry.WeakSmartValue as string;

            EditorGraphView editorGraphView = SelectedOwnerUtility.GetSelectedOwner(Property) as EditorGraphView;
            if (editorGraphView == null) return;
            EditorUniversalGraphAsset universalGraphAsset = editorGraphView.graphAsset as EditorUniversalGraphAsset;
            if (universalGraphAsset == null) return;

            string labelString = key;
            if (string.IsNullOrEmpty(labelString) == false)
            {
                EditorParameter parameter = universalGraphAsset.editorParametersManage?.GetParameter(key);
                if (parameter != null) labelString = parameter.description;
            }

            GUILayout.BeginHorizontal();

            if (label != null) GUILayout.Label(label);

            if (GUILayout.Button(labelString, "MiniPopup", GUILayout.MinWidth(100f)))
            {
                VariableKeyTypeFilterAttribute filterAttribute = Property.GetAttribute<VariableKeyTypeFilterAttribute>();

                OdinMenu odinMenu = new("选择参数");

                if (universalGraphAsset.editorParametersManage != null)
                {
                    for (var i = 0; i < universalGraphAsset.editorParametersManage.parameters.Count; i++)
                    {
                        EditorParameter parameter = universalGraphAsset.editorParametersManage.parameters[i];
                        if (filterAttribute != null && filterAttribute.type != parameter.value.type) continue;
                        odinMenu.AddItem(parameter.description, () => Property.ValueEntry.WeakSmartValue = parameter.key);
                    }
                }

                odinMenu.ShowInPopup();
            }

            GUILayout.EndHorizontal();
        }
    }
}