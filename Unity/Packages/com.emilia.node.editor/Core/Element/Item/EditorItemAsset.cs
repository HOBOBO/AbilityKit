using System;
using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Kit.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 编辑器Item资源
    /// </summary>
    [Serializable, SelectedClear]
    public abstract class EditorItemAsset : TitleAsset, IUnityAsset
    {
        [SerializeField, HideInInspector]
        private string _id;

        [SerializeField, HideInInspector]
        private Rect _position;

        [SerializeField, HideInInspector]
        private EditorGraphAsset _graphAsset;

        [NonSerialized]
        private PropertyTree _propertyTree;

        /// <summary>
        /// Id
        /// </summary>
        public string id
        {
            get => this._id;
            set => this._id = value;
        }

        /// <summary>
        /// 位置
        /// </summary>
        public Rect position
        {
            get => this._position;
            set => this._position = value;
        }

        /// <summary>
        /// 所属的GrpahAsset
        /// </summary>
        public EditorGraphAsset graphAsset
        {
            get => this._graphAsset;
            set => this._graphAsset = value;
        }

        /// <summary>
        /// 自身Odin属性树
        /// </summary>
        public PropertyTree propertyTree
        {
            get
            {
                if (_propertyTree == null) _propertyTree = PropertyTree.Create(this);
                return _propertyTree;
            }
        }

        protected virtual void OnEnable() { }

        public virtual void SetChildren(List<Object> childAssets) { }
        public virtual List<Object> GetChildren() => null;

        protected virtual void OnDisable()
        {
            _propertyTree?.Dispose();
            _propertyTree = null;
        }
    }
}