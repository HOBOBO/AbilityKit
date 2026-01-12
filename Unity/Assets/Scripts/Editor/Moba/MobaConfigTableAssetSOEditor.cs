#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CustomEditor(typeof(MobaConfigTableAssetSO), true)]
    public sealed class MobaConfigTableAssetSOEditor : EditorBase
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Export Config Json"))
            {
                var assetPath = AssetDatabase.GetAssetPath(target);
                var folder = AssetDatabase.IsValidFolder(assetPath)
                    ? assetPath
                    : Path.GetDirectoryName(assetPath)?.Replace('\\', '/');

                MobaConfigJsonExporter.ExportFromFolder(folder);
            }

            DrawInspectorBody();
        }
    }

#if ODIN_INSPECTOR
    public abstract class EditorBase : Sirenix.OdinInspector.Editor.OdinEditor
    {
        protected void DrawInspectorBody()
        {
            base.OnInspectorGUI();
        }
    }
#else
    public abstract class EditorBase : UnityEditor.Editor
    {
        protected void DrawInspectorBody()
        {
            DrawDefaultInspector();
        }
    }
#endif
}
#endif
