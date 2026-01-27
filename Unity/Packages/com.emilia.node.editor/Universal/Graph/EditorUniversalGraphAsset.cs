using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Node.Editor;
using Emilia.Variables.Editor;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用EditorGraphAsset实现
    /// </summary>
    public abstract class EditorUniversalGraphAsset : EditorGraphAsset, IObjectDescription
    {
        [LabelText("描述"), TextArea(3, 10)]
        public string description;

        /// <summary>
        /// 自定义参数
        /// </summary>
        [NonSerialized, OdinSerialize, HideInInspector]
        public EditorParametersManager editorParametersManage;

        string IObjectDescription.description => description;

        /// <summary>
        /// 操作菜单标签
        /// </summary>
        public virtual string[] operateMenuTags => new[] {OperateMenuTagDefine.BaseActionTag, OperateMenuTagDefine.UniversalActionTag};

        public override void SetChildren(List<Object> childAssets)
        {
            base.SetChildren(childAssets);

            EditorParametersManager parametersManage = childAssets.OfType<EditorParametersManager>().FirstOrDefault();
            if (parametersManage == null) return;

            if (this.editorParametersManage != null) DestroyImmediate(this.editorParametersManage);

            this.editorParametersManage = parametersManage;
            EditorAssetKit.SaveAssetIntoObject(this.editorParametersManage, this);
        }

        public override List<Object> GetChildren()
        {
            var assets = base.GetChildren();
            if (this.editorParametersManage != null) assets.Add(this.editorParametersManage);
            return assets;
        }
    }
}