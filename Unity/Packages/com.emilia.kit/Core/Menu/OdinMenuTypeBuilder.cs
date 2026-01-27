#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Emilia.Kit.Editor;
using UnityEditor;

namespace Emilia.Kit
{
    /// <summary>
    /// Type选择器构建器
    /// </summary>
    public class OdinMenuTypeBuilder
    {
        private string _title = OdinMenu.DefaultName;
        private float _width = OdinMenu.DefaultWidth;
        private Type _baseType;

        internal OdinMenuTypeBuilder(Type baseType)
        {
            _baseType = baseType;
        }

        public OdinMenuTypeBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public OdinMenuTypeBuilder WithWidth(float width)
        {
            _width = width;
            return this;
        }

        public void ShowForType(Action<Type> onSelected)
        {
            ShowInPopupType(_baseType, _title, _width, onSelected);
        }

        public void ShowForInstance(Action<object> onSelected)
        {
            ShowInPopupType(_baseType, _title, _width, (type) => onSelected?.Invoke(ReflectUtility.CreateInstance(type)));
        }

        private static void ShowInPopupType(Type objectType, string title, float width, Action<Type> onSelected)
        {
            OdinMenu menu = new(title);

            IList<Type> types = TypeCache.GetTypesDerivedFrom(objectType);
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface) continue;

                if (HideUtility.IsHide(type)) continue;

                string displayName = type.Name;
                string description = ObjectDescriptionUtility.GetDescription(type);
                if (string.IsNullOrEmpty(description) == false) displayName = description;

                menu.AddItem(displayName, () => onSelected(type));
            }

            menu.ShowInPopup(width);
        }
    }
}
#endif