#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    /// <summary>
    /// 弹窗构建器，用于配置和显示资源选择菜单
    /// </summary>
    public class OdinMenuBuilder<TResource, TOutput> where TResource : Object
    {
        private string _title = OdinMenu.DefaultName;
        private float _width = OdinMenu.DefaultWidth;
        private Func<TResource, string> _getDescription;
        private Func<TResource, TOutput> _selector;
        private IEnumerable<TResource> _resources;

        internal OdinMenuBuilder() { }

        public OdinMenuBuilder<TResource, TOutput> WithResources(IEnumerable<TResource> resources)
        {
            _resources = resources;
            return this;
        }

        public OdinMenuBuilder<TResource, TOutput> WithDescription(Func<TResource, string> getDescription)
        {
            _getDescription = getDescription;
            return this;
        }

        public OdinMenuBuilder<TResource, TOutput> WithSelector(Func<TResource, TOutput> selector)
        {
            _selector = selector;
            return this;
        }

        public OdinMenuBuilder<TResource, TOutput> WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public OdinMenuBuilder<TResource, TOutput> WithWidth(float width)
        {
            _width = width;
            return this;
        }

        public void Show(Action<TOutput> onSelected)
        {
            if (_resources == null) throw new InvalidOperationException("Resources not set. Call WithResources first.");
            if (_selector == null) throw new InvalidOperationException("Selector not set. Call WithSelector first.");

            ShowInPopupList(_title, _width, _resources, _getDescription, _selector, onSelected);
        }

        private static void ShowInPopupList<TAsset, TOut>(
            string title,
            float width,
            IEnumerable<TAsset> assets,
            Func<TAsset, string> getDescription,
            Func<TAsset, TOut> selectValue,
            Action<TOut> onSelected)
            where TAsset : Object
        {
            OdinMenu menu = new(title);

            foreach (TAsset asset in assets)
            {
                if (asset == null) continue;
                if (HideUtility.IsHide(asset)) continue;

                string displayName = asset.name;
                string description = getDescription?.Invoke(asset) ?? string.Empty;
                if (string.IsNullOrEmpty(description) == false) displayName += $"({description})";

                TOut value = selectValue(asset);

                menu.AddItem(displayName, () => onSelected(value));
            }

            menu.ShowInPopup(width);
        }
    }
}
#endif