#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using Action = System.Action;

namespace Emilia.Kit
{
    public class OdinMenu
    {
        public const string DefaultName = "选择";
        public const float DefaultWidth = 200f;

        private struct MenuItem
        {
            public object userData;
            public Action<object> action;
        }

        private Dictionary<string, MenuItem> items = new();

        public string title { get; set; }

        public float defaultWidth { get; set; } = DefaultWidth;

        public OdinMenu(string name = DefaultName)
        {
            title = name;
        }

        public bool HasItem(string name) => items.ContainsKey(name);

        public void AddItem(string name, Action action)
        {
            MenuItem menuItem = new();
            menuItem.action = (_) => action?.Invoke();
            items[name] = menuItem;
        }

        public void AddItem(string name, object userData, Action<object> action)
        {
            MenuItem menuItem = new();
            menuItem.userData = userData;
            menuItem.action = action;
            items[name] = menuItem;
        }

        public OdinEditorWindow ShowInPopup() => ShowInPopup(defaultWidth);

        public OdinEditorWindow ShowInPopup(float width)
        {
            GenericSelector<Action> customGenericSelector = GetSelector();
            return customGenericSelector.ShowInPopup(width);
        }

        public OdinEditorWindow ShowInPopup(Rect rect, float width)
        {
            GenericSelector<Action> customGenericSelector = GetSelector();
            return customGenericSelector.ShowInPopup(rect, width);
        }

        private GenericSelector<Action> GetSelector()
        {
            IEnumerable<GenericSelectorItem<Action>> customCollection = items.Keys.Select(itemName =>
                new GenericSelectorItem<Action>($"{itemName}", () => items[itemName].action(items[itemName].userData)));

            GenericSelector<Action> customGenericSelector = new(title, false, customCollection);

            customGenericSelector.SelectionTree.SetSearchFunction(item => {
                string target = item.SearchString;
                string input = customGenericSelector.SelectionTree.Config.SearchTerm;
                return SearchUtility.SmartSearch(target, input);
            });

            customGenericSelector.EnableSingleClickToSelect();

            customGenericSelector.SelectionChanged += ints => {
                Action result = ints.FirstOrDefault();
                if (result != null) result();
            };

            return customGenericSelector;
        }

        public void Clear()
        {
            items.Clear();
        }
    }
}
#endif