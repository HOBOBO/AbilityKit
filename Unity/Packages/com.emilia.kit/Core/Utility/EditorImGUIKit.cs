#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Reflection.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using UIElementsUtility_Internals = Sirenix.Reflection.Editor.UIElementsUtility_Internals;

namespace Emilia.Kit.Editor
{
    public static class EditorImGUIKit
    {
        public readonly static Rect DummyRect = new Rect(0, 0, 1, 1);
        
        public static EditorWindow GetImGUIWindow()
        {
            IMGUIContainer currentImguiContainer = UIElementsUtility_Internals.GetCurrentIMGUIContainer();

            VisualElement rootVisualContainer = currentImguiContainer;
            while (rootVisualContainer.parent != null)
            {
                rootVisualContainer = rootVisualContainer.parent;
                if (rootVisualContainer.name.Contains("rootVisualContainer")) break;
            }

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window.rootVisualElement == rootVisualContainer) return window;
            }

            return null;
        }

        public static EditorWindow CreateWindow(Type type)
        {
            ScriptableObject instance = ScriptableObject.CreateInstance(type);
            EditorWindow window = instance as EditorWindow;
            window.Show();
            return window;
        }

        public static T OpenWindow<T>(string name, int width, int height, bool utility = false) where T : EditorWindow
        {
            if (EditorWindow.HasOpenInstances<T>())
            {
                T window = EditorWindow.GetWindow<T>(utility, name);
                window.Focus();
                return window;
            }
            else
            {
                T window = EditorWindow.GetWindow<T>(utility, name);
                window.position = GUIHelper.GetEditorWindowRect().AlignCenter(width, height);
                return window;
            }
        }

        public static T AddToWindow<T>(string name, params Type[] windowTypes) where T : EditorWindow
        {
            T window = null;
            if (EditorWindow.HasOpenInstances<T>() == false)
            {
                window = EditorWindow.CreateWindow<T>(name, windowTypes);
            }
            else
            {
                window = EditorWindow.GetWindow<T>(name);
                window.Focus();
            }
            return window;
        }

        public static void CloseWindow<T>() where T : EditorWindow
        {
            T[] windows = Resources.FindObjectsOfTypeAll<T>();
            int amount = windows.Length;
            for (int i = 0; i < amount; i++)
            {
                T window = windows[i];
                window.Close();
            }
        }

        public static EditorWindow FindEditorWindowOfType(Type type)
        {
            Object[] results = Resources.FindObjectsOfTypeAll(type);
            return results.FirstOrDefault() as EditorWindow;
        }

        public static void RepaintHierarchy()
        {
            List<EditorWindow> windows = SceneHierarchyWindow_Internals.GetAllSceneHierarchyWindows_Internals();
            int count = windows.Count;
            for (int i = 0; i < count; i++)
            {
                EditorWindow window = windows[i];
                if (window != null) window.Repaint();
            }
        }
        public static void ShowNotification(string tips)
        {
            EditorWindow window = GetImGUIWindow();
            if (window != null) window.ShowNotification(new GUIContent(tips));
        }

        public static void ShowTypePopup<T>(Func<Type, string> nameGetter, Action<Type> onSelector)
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<T>().Where(t => t.IsAbstract == false).ToArray();
            Dictionary<string, Type> dataForDraw = new Dictionary<string, Type>();

            int amount = types.Length;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                string name = nameGetter(type);
                dataForDraw.Add(name, type);
            }

            IEnumerable<GenericSelectorItem<Type>> customCollection = dataForDraw.Keys.Select(itemName =>
                new GenericSelectorItem<Type>($"{itemName}", dataForDraw[itemName]));

            GenericSelector<Type> customGenericSelector = new("选择", false, customCollection);

            customGenericSelector.EnableSingleClickToSelect();
            customGenericSelector.SelectionChanged += ints => {
                Type result = ints.FirstOrDefault();
                if (result != default) onSelector?.Invoke(result);
            };

            customGenericSelector.ShowInPopup(200);
        }

        public static void InputDropDown(Action<string> inputCallback, string startInput = "", float width = 300)
        {
            OdinEditorWindow window = null;
            inputCallback += (_) => { window.Close(); };
            InputContainer inputData = new InputContainer();
            inputData.inputCallback = inputCallback;
            inputData.inputString = startInput;
            Rect rect = new Rect(Event.current.mousePosition.x - width / 2f, Event.current.mousePosition.y, 0, 0);
            window = OdinEditorWindow.InspectObjectInDropDown(inputData, rect, width);
        }

        public static void InputWindow(Action<string> inputCallback, string startInput = "", string titleString = "输入", float width = 300)
        {
            Rect position = GUIHelper.GetEditorWindowRect().AlignCenter(width, 100);
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow != null) position = focusedWindow.position.AlignCenter(width, 100);

            OdinEditorWindow window = null;
            inputCallback += (_) => { window.Close(); };
            InputContainer inputData = new InputContainer();
            inputData.inputCallback = inputCallback;
            inputData.inputString = startInput;
            window = OdinEditorWindow.InspectObject(inputData);
            window.titleContent = new GUIContent(titleString);
            window.position = position;
        }

        [Serializable]
        private class InputContainer
        {
            public Action<string> inputCallback;

            [LabelText("输入：")]
            public string inputString;

            [Button("输入确认")]
            public void InputConfirm()
            {
                inputCallback?.Invoke(inputString);
            }
        }
    }
}
#endif