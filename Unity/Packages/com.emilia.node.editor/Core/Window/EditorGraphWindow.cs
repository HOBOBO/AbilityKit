using System;
using Emilia.Kit.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using Sirenix.Serialization;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 通用Graph的Window
    /// </summary>
    public class EditorGraphWindow : OdinProEditorWindow, IEditorAssetWindow
    {
        [NonSerialized, OdinSerialize]
        private EditorGraphImGUIRoot _graphImGUIRoot;

        public EditorGraphAsset graphAsset => this._graphImGUIRoot?.asset;
        public string id => GetId(graphAsset);

        public void OnReOpen(object arg)
        {
            if (graphAsset == null)
            {
                Focus();
                return;
            }

            WindowSettingsAttribute settings = graphAsset.GetType().GetAttribute<WindowSettingsAttribute>();
            if (settings == null)
            {
                Focus();
                return;
            }

            if (settings.openMode == OpenWindowMode.Asset) Focus();
            else
            {
                EditorGraphAsset editorGraphAsset = arg as EditorGraphAsset;
                if (editorGraphAsset != null) SetGraphAsset(editorGraphAsset);
            }
        }

        public void OnOpen(object arg)
        {
            EditorGraphAsset editorGraphAsset = arg as EditorGraphAsset;
            if (editorGraphAsset != null)
            {
                position = GetPosition(editorGraphAsset);

                SetGraphAsset(editorGraphAsset);
            }
        }

        private static Rect GetPosition(EditorGraphAsset graphAsset)
        {
            WindowSettingsAttribute settings = graphAsset.GetType().GetAttribute<WindowSettingsAttribute>();
            if (settings == null) return GUIHelper.GetEditorWindowRect().AlignCenter(850, 600);

            if (string.IsNullOrEmpty(settings.getStartSizeExpression)) return GUIHelper.GetEditorWindowRect().AlignCenter(settings.startSize.x, settings.startSize.y);

            ValueResolver<Vector2> valueResolver = ValueResolver.Get<Vector2>(graphAsset.propertyTree.RootProperty, settings.getStartSizeExpression);
            if (valueResolver.HasError)
            {
                Debug.LogError($"GetPosition Error: {valueResolver.ErrorMessage}");
                return GUIHelper.GetEditorWindowRect().AlignCenter(850, 600);
            }

            Vector2 size = valueResolver.GetValue();

            return GUIHelper.GetEditorWindowRect().AlignCenter(size.x, size.y);
        }

        private void OnEnable()
        {
            IMGUIContainer container = new(Draw);
            container.style.flexGrow = 1;
            rootVisualElement.Add(container);
        }

        private void OnInspectorUpdate()
        {
            if (graphAsset != null) EditorAssetWindowUtility.Refresh(this);
            UpdateTitle();
        }

        private void Draw()
        {
            this._graphImGUIRoot?.OnImGUI(position.height);
        }

        private void UpdateTitle()
        {
            if (graphAsset == null)
            {
                titleContent.text = "Graph";
                return;
            }

            titleContent.text = graphAsset.name;

            WindowSettingsAttribute settings = graphAsset.GetType().GetAttribute<WindowSettingsAttribute>();
            if (settings != null)
            {
                if (string.IsNullOrEmpty(settings.titleExpression))
                {
                    titleContent.text = settings.title;
                    return;
                }

                ValueResolver<string> valueResolver = ValueResolver.Get<string>(graphAsset.propertyTree.RootProperty, settings.titleExpression);
                if (valueResolver.HasError)
                {
                    Debug.LogError($"UpdateTitle Error: {valueResolver.ErrorMessage}");
                    return;
                }

                string getTitle = valueResolver.GetValue();
                titleContent.text = getTitle;
            }

            if (this._graphImGUIRoot?.graphView?.graphSave?.dirty ?? false) titleContent.text += "*";
        }

        /// <summary>
        /// 设置资源
        /// </summary>
        public void SetGraphAsset(EditorGraphAsset graphAsset)
        {
            if (this._graphImGUIRoot == null)
            {
                this._graphImGUIRoot = new EditorGraphImGUIRoot();
                this._graphImGUIRoot.Initialize(this);
            }

            this._graphImGUIRoot.SetAsset(graphAsset);
        }

        private void OnDisable()
        {
            this._graphImGUIRoot?.Dispose();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            this._graphImGUIRoot = null;
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceID, int line)
        {
            EditorGraphAsset asset = EditorUtility.InstanceIDToObject(instanceID) as EditorGraphAsset;
            if (asset == null) return false;

            string id = GetId(asset);
            Type windowType = GetWindowType(asset.GetType());

            EditorAssetWindowUtility.OpenWindow(windowType, id, asset);

            return true;
        }

        private static Type GetWindowType(Type assetType)
        {
            WindowSettingsAttribute settings = assetType.GetAttribute<WindowSettingsAttribute>();
            if (settings == null) return typeof(EditorGraphWindow);
            return settings.windowType ?? typeof(EditorGraphWindow);
        }

        /// <summary>
        /// 获取窗口ID
        /// </summary>
        public static string GetId(EditorGraphAsset editorGraphAsset)
        {
            const string SingleKey = "{SingleWindow}";

            if (editorGraphAsset == null) return string.Empty;
            WindowSettingsAttribute settings = editorGraphAsset.GetType().GetAttribute<WindowSettingsAttribute>();
            if (settings == null) return editorGraphAsset.id;

            switch (settings.openMode)
            {
                case OpenWindowMode.Asset:
                    return editorGraphAsset.id;
                case OpenWindowMode.Type:
                    return editorGraphAsset.GetType().FullName;
                case OpenWindowMode.Single:
                    return SingleKey;
            }

            return string.Empty;
        }
    }
}