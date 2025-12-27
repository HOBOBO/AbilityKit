//------------------------------------------------------------
//        File:  ColorGroup.cs
//       Brief:  ColorGroup
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-27
//============================================================

using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Group = UnityEditor.Experimental.GraphView.Group;

namespace BTCore.Editor
{
    public class ColorGroup : Group
    {
        public ColorGroup() {
            var colorField = new ColorField() { value = new Color(0.7f, 0.8f, 0.7f) };
            colorField.RegisterValueChangedCallback(evt => ApplyColor(evt.newValue));
            colorField.style.width = 50;
            colorField.style.height = 20;
            headerContainer.Add(colorField);
            ApplyColor(colorField.value);
        }

        private void ApplyColor(Color color) {
            style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.3f));
        }
    }
}