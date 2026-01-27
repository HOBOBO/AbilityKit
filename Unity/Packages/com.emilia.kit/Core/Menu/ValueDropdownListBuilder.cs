#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Object = UnityEngine.Object;

namespace Emilia.Kit
{
    /// <summary>
    /// ValueDropdown构建器，用于配置和生成下拉列表
    /// </summary>
    public class ValueDropdownBuilder<TResource, TOutput> where TResource : Object
    {
        private Func<TResource, string> _getDescription;
        private Func<TResource, TOutput> _selector;
        private IEnumerable<TResource> _resources;

        internal ValueDropdownBuilder() { }

        public ValueDropdownBuilder<TResource, TOutput> WithResources(IEnumerable<TResource> resources)
        {
            _resources = resources;
            return this;
        }

        public ValueDropdownBuilder<TResource, TOutput> WithDescription(Func<TResource, string> getDescription)
        {
            _getDescription = getDescription;
            return this;
        }

        public ValueDropdownBuilder<TResource, TOutput> WithSelector(Func<TResource, TOutput> selector)
        {
            _selector = selector;
            return this;
        }

        public ValueDropdownList<TOutput> Build()
        {
            if (_resources == null) throw new InvalidOperationException("Resources not set. Call WithResources first.");
            if (_selector == null) throw new InvalidOperationException("Selector not set. Call WithSelector first.");

            return BuildList(_resources, _getDescription, _selector);
        }

        private static ValueDropdownList<TOut> BuildList<TAsset, TOut>(
            IEnumerable<TAsset> assets,
            Func<TAsset, string> getDescription,
            Func<TAsset, TOut> selectValue)
            where TAsset : Object
        {
            ValueDropdownList<TOut> list = new();

            foreach (TAsset asset in assets)
            {
                if (asset == null) continue;
                if (HideUtility.IsHide(asset)) continue;

                string displayName = asset.name;
                string description = getDescription?.Invoke(asset) ?? string.Empty;
                if (string.IsNullOrEmpty(description) == false) displayName += $"({description})";

                list.Add(displayName, selectValue(asset));
            }

            return list;
        }
    }
}
#endif