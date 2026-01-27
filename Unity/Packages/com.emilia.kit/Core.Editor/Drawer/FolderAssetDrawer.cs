using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public class FolderAssetDrawer : OdinValueDrawer<FolderAsset>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            FolderAsset value = this.ValueEntry.SmartValue;

            GUILayout.BeginHorizontal();

            GUILayout.Label(label);

            GUIContent content = new GUIContent();
            if (value.folder == null)
            {
                content.text = "None";
            }
            else
            {
                content.text = value.folder.name;
                content.tooltip = value.unityPath;
                label.tooltip = value.unityPath;
            }

            if (GUILayout.Button(content))
            {
                if (value.folder != null)
                {
                    EditorGUIUtility.PingObject(value.folder);
                    Selection.activeObject = value.folder;
                }
            }

            if (GUILayout.Button("", "MiniPopup", GUILayout.Width(20)))
            {
                OdinMenu odinMenu = new OdinMenu("选择文件夹");

                DefaultAsset[] defaultAssets = EditorAssetKit.GetEditorResources<DefaultAsset>();
                int amount = defaultAssets.Length;
                for (int i = 0; i < amount; i++)
                {
                    DefaultAsset asset = defaultAssets[i];
                    string path = AssetDatabase.GetAssetPath(asset);
                    if (AssetDatabase.IsValidFolder(path) == false) continue;
                    odinMenu.AddItem(path, () => { value.folder = asset; });
                }

                odinMenu.ShowInPopup();
            }

            GUILayout.EndHorizontal();

            DragArea(value);
        }

        private void DragArea(FolderAsset value)
        {
            Event e = Event.current;
            Rect rect = GUILayoutUtility.GetLastRect();
            if (e.type is not (EventType.DragUpdated or EventType.DragPerform) || ! rect.Contains(e.mousePosition)) return;

            DefaultAsset defaultAsset = DragAndDrop.objectReferences.OfType<DefaultAsset>().FirstOrDefault();
            if (defaultAsset == null) return;

            string assetPath = AssetDatabase.GetAssetPath(defaultAsset);
            bool isFolder = AssetDatabase.IsValidFolder(assetPath);
            DragAndDrop.visualMode = isFolder ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (e.type != EventType.DragPerform) return;

            DragAndDrop.AcceptDrag();
            if (isFolder) value.folder = defaultAsset;
        }
    }
}