using System;
using Emilia.Kit.Editor;
using Emilia.Node.Attributes;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 将EditorGraphAsset序列化绘制为GraphView
    /// </summary>
    public class EditorAssetShowAttributeDrawer : OdinAttributeDrawer<EditorAssetShowAttribute>, IDisposable
    {
        private EditorGraphImGUIRoot _graphImGUIRoot;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            EditorAssetShowAttribute attribute = Attribute;

            if (this._graphImGUIRoot == null) this._graphImGUIRoot = new EditorGraphImGUIRoot();

            if (this._graphImGUIRoot.window == null)
            {
                EditorWindow window = EditorImGUIKit.GetImGUIWindow();
                this._graphImGUIRoot.Initialize(window);
            }

            if (this._graphImGUIRoot.asset == null)
            {
                CallNextDrawer(label);
                EditorGraphAsset asset = Property.ValueEntry.WeakSmartValue as EditorGraphAsset;
                if (asset != null) this._graphImGUIRoot.SetAsset(asset);
                return;
            }

            if (this._graphImGUIRoot == null || this._graphImGUIRoot.window == null) return;

            this._graphImGUIRoot.OnImGUI(attribute.height, attribute.width);
        }

        public void Dispose()
        {
            if (this._graphImGUIRoot != null)
            {
                if (this._graphImGUIRoot.asset != null) this._graphImGUIRoot.asset.SaveAll();
                this._graphImGUIRoot.Dispose();
            }

            this._graphImGUIRoot = null;
        }
    }
}