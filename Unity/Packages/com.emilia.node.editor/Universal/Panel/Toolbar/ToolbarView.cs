using System;
using System.Collections.Generic;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 工具栏面板
    /// </summary>
    public class ToolbarView : GraphPanel
    {
        protected float _leftMargin = 5;
        protected float _rightMargin = 5f;

        protected Dictionary<ToolbarViewControlPosition, List<IToolbarViewControl>> controls = new();

        /// <summary>
        /// 方向
        /// </summary>
        public ToolbarViewOrientation orientation { get; set; } = ToolbarViewOrientation.Horizontal;

        /// <summary>
        /// 左边距
        /// </summary>
        public float leftMargin => _leftMargin;

        /// <summary>
        /// 右边距
        /// </summary>
        public float rightMargin => _rightMargin;

        public ToolbarView()
        {
            name = nameof(ToolbarView);
            Add(new IMGUIContainer(OnImGUI));
        }

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);

            InitControls();
            InitAttributeControls();

            if (parentView != null) parentView.canResizable = false;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public override void Dispose()
        {
            base.Dispose();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        /// <summary>
        /// 设置边距
        /// </summary>
        public void SetMargins(float size)
        {
            this._leftMargin = size;
            this._rightMargin = size;
        }

        protected void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            ReInitControls();
        }

        public void ReInitControls()
        {
            controls.Clear();
            InitControls();
            InitAttributeControls();
        }

        protected virtual void InitControls() { }

        protected void InitAttributeControls()
        {
            IList<Type> types = TypeCache.GetTypesDerivedFrom<ToolbarViewControlAttributeHandle>();
            int count = types.Count;
            for (int i = 0; i < count; i++)
            {
                Type type = types[i];
                if (type.IsAbstract) continue;

                ToolbarViewControlAttributeHandle attributeHandle = Activator.CreateInstance(type) as ToolbarViewControlAttributeHandle;
                attributeHandle.OnHandle(this, graphView);
            }
        }

        /// <summary>
        /// 添加控件
        /// </summary>
        public void AddControl(IToolbarViewControl control, ToolbarViewControlPosition position = ToolbarViewControlPosition.LeftOrTop)
        {
            if (controls.TryGetValue(position, out var list) == false) controls[position] = list = new List<IToolbarViewControl>();
            list.Add(control);
        }

        protected virtual void OnImGUI()
        {
            if (orientation == ToolbarViewOrientation.Horizontal) GUILayout.BeginHorizontal(EditorStyles.toolbar);
            else GUILayout.BeginVertical();

            GUILayout.Space(this._leftMargin);

            DrawControls(ToolbarViewControlPosition.LeftOrTop);

            GUILayout.FlexibleSpace();

            DrawControls(ToolbarViewControlPosition.Center);

            GUILayout.FlexibleSpace();

            DrawControls(ToolbarViewControlPosition.RightOrBottom);

            GUILayout.Space(this._rightMargin);

            if (orientation == ToolbarViewOrientation.Horizontal) GUILayout.EndHorizontal();
            else GUILayout.EndVertical();
        }

        protected virtual void DrawControls(ToolbarViewControlPosition controlPosition)
        {
            List<IToolbarViewControl> drawControls = controls.GetValueOrDefault(controlPosition);
            if (drawControls == null) return;

            for (var i = 0; i < drawControls.Count; i++)
            {
                IToolbarViewControl control = drawControls[i];
                control.OnDraw();
            }
        }
    }
}