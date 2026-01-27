using System.Collections;
using System.Collections.Generic;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;
using Sirenix.Utilities;
using Unity.EditorCoroutines.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用节点表现元素实现
    /// </summary>
    [EditorNode(typeof(UniversalNodeAsset))]
    public class UniversalEditorNodeView : EditorNodeView
    {
        protected EditorCoroutine focusCoroutine;
        protected VisualElement focusBorder;

        protected UniversalNodeAsset _universalNodeAsset;

        protected VisualElement horizontalContainer;
        protected VisualElement horizontalInputContainer;
        protected VisualElement horizontalOutputContainer;

        protected VisualElement verticalInputContainer;
        protected VisualElement verticalOutputContainer;

        protected TextField titleTextField;

        protected NodeMessageButtonElement messageButtonElement;
        protected NodeMessageContainer messageContainer;
        protected List<NodeMessageElement> messageElements = new();

        protected Dictionary<string, NodeTipsElement> tipsElements = new();

        protected NodeDuplicateDragger duplicateDragger;
        protected NodeInsertDragger insertDragger;

        /// <summary>
        /// 可以展开
        /// </summary>
        public virtual bool canExpanded => true;

        protected virtual bool canRename => false;
        protected virtual Texture2D icon => null;
        protected override string styleFilePath => "Node/Styles/UniversalEditorNodeView.uss";

        public override void Initialize(EditorGraphView graphView, EditorNodeAsset asset)
        {
            base.Initialize(graphView, asset);
            this._universalNodeAsset = asset as UniversalNodeAsset;

            if ((capabilities & Capabilities.Renamable) != 0) InitializeRenamableTitle();

            UpdateTitle();

            duplicateDragger = new NodeDuplicateDragger();
            this.insertDragger = new NodeInsertDragger();

            this.AddManipulator(this.duplicateDragger);
            this.AddManipulator(this.insertDragger);
        }

        protected override void InitializeNodeView()
        {
            base.InitializeNodeView();

            if (canRename) capabilities |= Capabilities.Renamable;

            InitializeIcon();
            InitializeExpandButton();
            InitializeFocusBorder();
            InitializeMessage();
        }

        protected void SetNodeColor(object nodeAsset)
        {
            if (nodeAsset == null) return;
            NodeColorAttribute flowNodeColor = nodeAsset.GetType().GetCustomAttribute<NodeColorAttribute>(true);
            if (flowNodeColor == null) return;
            SetColor(new Color(flowNodeColor.r, flowNodeColor.g, flowNodeColor.b));
        }

        protected void SetNodeTips(object nodeAsset)
        {
            if (nodeAsset == null) return;
            NodeTipsAttribute tipsAttribute = nodeAsset.GetType().GetCustomAttribute<NodeTipsAttribute>(true);
            if (tipsAttribute == null) return;
            SetTooltip(tipsAttribute.tips);
        }

        protected void InitializeExpandButton()
        {
            if (canExpanded) return;

            VisualElement expandButton = this.Q("collapse-button");
            expandButton.style.display = DisplayStyle.None;
        }

        protected void InitializeIcon()
        {
            if (icon == null) return;
            VisualElement iconElement = new();
            iconElement.name = "title-icon";

            iconElement.style.backgroundImage = icon;
            titleContainer.Insert(0, iconElement);
        }

        protected void InitializeFocusBorder()
        {
            const float borderWidth = 2;

            focusBorder = new VisualElement();
            focusBorder.pickingMode = PickingMode.Ignore;
            focusBorder.name = "focus-border";

            RegisterCallback<GeometryChangedEvent>(_ => {
                focusBorder.style.width = layout.width + borderWidth;
                focusBorder.style.height = layout.height + borderWidth;
            });

            Add(focusBorder);
        }

        protected void InitializeRenamableTitle()
        {
            this.titleTextField = new TextField();
            titleTextField.style.flexGrow = 1;
            titleTextField.style.display = DisplayStyle.None;
            titleTextField.style.width = new StyleLength(StyleKeyword.Auto);
            titleTextField.style.maxWidth = 300;

            titleContainer.Insert(0, this.titleTextField);

            titleLabel.RegisterCallback<MouseDownEvent>(e => {
                if (e.clickCount == 2 && e.button == 0) StartRenaming();
            });

            titleTextField.RegisterCallback<FocusOutEvent>(_ => EndRenaming(titleTextField.value));

            void StartRenaming()
            {
                titleTextField.style.minWidth = titleLabel.layout.width;
                titleTextField.style.display = DisplayStyle.Flex;
                titleLabel.style.display = DisplayStyle.None;
                titleTextField.focusable = true;

                titleTextField.SetValueWithoutNotify(title);
                titleTextField.Focus();
                titleTextField.SelectAll();
            }

            void EndRenaming(string newName)
            {
                titleTextField.style.display = DisplayStyle.None;
                titleLabel.style.display = DisplayStyle.Flex;
                titleTextField.focusable = false;

                RegisterCompleteObjectUndo("Graph Rename");
                _universalNodeAsset.displayName = newName;

                UpdateTitle();
            }
        }

        protected void InitializeMessage()
        {
            messageButtonElement = new NodeMessageButtonElement(SwitchMessageContainerState);
            messageButtonElement.style.display = DisplayStyle.None;

            titleButtonContainer.Add(messageButtonElement);

            messageContainer = new NodeMessageContainer();
            messageContainer.style.display = DisplayStyle.None;

            topLayerContainer.Add(messageContainer);
        }

        protected void SwitchMessageContainerState()
        {
            if (messageContainer.style.display == DisplayStyle.None) messageContainer.style.display = DisplayStyle.Flex;
            else messageContainer.style.display = DisplayStyle.None;
        }

        public override void OnValueChanged(bool isSilent = false)
        {
            base.OnValueChanged(isSilent);
            UpdateTitle();
        }

        /// <summary>
        /// 更新标题
        /// </summary>
        public virtual void UpdateTitle()
        {
            title = this._universalNodeAsset.title;
        }

        public override List<EditorPortInfo> CollectStaticPortAssets() => new();

        public override IEditorPortView AddPortView(int index, EditorPortInfo info)
        {
            IEditorPortView portView = base.AddPortView(index, info);

            switch (portView.editorOrientation)
            {
                case EditorOrientation.Horizontal:
                    if (horizontalContainer == null) CreateHorizontalContainer();
                    if (portView.portDirection == EditorPortDirection.Input) this.horizontalInputContainer.Insert(index, portView.portElement);
                    else if (portView.portDirection == EditorPortDirection.Output) this.horizontalOutputContainer.Insert(index, portView.portElement);
                    break;
                case EditorOrientation.Vertical:
                    if (verticalInputContainer == null && verticalOutputContainer == null) CreateVerticalContainer();
                    if (portView.portDirection == EditorPortDirection.Input) this.verticalInputContainer.Insert(index, portView.portElement);
                    else if (portView.portDirection == EditorPortDirection.Output) this.verticalOutputContainer.Insert(index, portView.portElement);
                    break;
                case EditorOrientation.Custom:
                    AddCustomPortView(index, portView, info);
                    break;
            }

            return portView;
        }

        protected void CreateHorizontalContainer()
        {
            NodeHorizontalContainer nodeHorizontalContainer = new();
            this.horizontalContainer = nodeHorizontalContainer;
            this.horizontalInputContainer = nodeHorizontalContainer.inputContainer;
            this.horizontalOutputContainer = nodeHorizontalContainer.outputContainer;
            portNodeBottomContainer.Add(nodeHorizontalContainer);
        }

        protected void CreateVerticalContainer()
        {
            NodeVerticalContainer inputVerticalContainer = new();
            this.verticalInputContainer = inputVerticalContainer;
            nodeTopContainer.Add(inputVerticalContainer);

            NodeVerticalContainer outputVerticalContainer = new();
            this.verticalOutputContainer = outputVerticalContainer;
            nodeBottomContainer.Add(outputVerticalContainer);
        }

        /// <summary>
        /// 添加自定义端口视图
        /// </summary>
        protected virtual void AddCustomPortView(int index, IEditorPortView portView, EditorPortInfo info) { }

        /// <summary>
        /// 添加消息
        /// </summary>
        public NodeMessageElement AddMessage(string message, NodeMessageLevel level)
        {
            NodeMessageElement nodeMessageElement = new();
            nodeMessageElement.Init(message, level);

            nodeMessageElement.onRemove += () => RemoveMessage(nodeMessageElement);

            messageElements.Add(nodeMessageElement);
            messageContainer.Add(nodeMessageElement);

            UpdateMessageButtonState();

            return nodeMessageElement;
        }

        /// <summary>
        /// 移除消息
        /// </summary>
        public void RemoveMessage(NodeMessageElement nodeMessageElement)
        {
            if (this.messageElements.Remove(nodeMessageElement) == false) return;
            nodeMessageElement.RemoveFromHierarchy();
            UpdateMessageButtonState();
        }

        protected void UpdateMessageButtonState()
        {
            if (this.messageElements.Count == 0) this.messageButtonElement.style.display = DisplayStyle.None;
            else
            {
                NodeMessageLevel maxLevel = GetMaxLevel();
                this.messageButtonElement.SetLevel(maxLevel);

                this.messageButtonElement.style.display = DisplayStyle.Flex;
            }
        }

        protected NodeMessageLevel GetMaxLevel()
        {
            NodeMessageLevel maxLevel = NodeMessageLevel.Info;
            int count = messageElements.Count;
            for (int i = 0; i < count; i++)
            {
                NodeMessageElement messageElement = messageElements[i];
                if (messageElement.level > maxLevel) maxLevel = messageElement.level;
            }

            return maxLevel;
        }

        /// <summary>
        /// 弹出Tips
        /// </summary>
        public void Tips(string text, long timeMs = 1500, float speed = 10)
        {
            if (tipsElements.ContainsKey(text)) return;

            NodeTipsElement nodeTipsElement = new();
            nodeTipsElement.text = text;

            tipsElements[text] = nodeTipsElement;

            schedule.Execute(() => {

                Add(nodeTipsElement);
                Vector3 titleCenter = titleContainer.layout.center;
                nodeTipsElement.Init(speed, titleCenter);

            }).ExecuteLater(1);

            schedule.Execute(() => RemoveTips(text)).ExecuteLater(timeMs);
        }

        /// <summary>
        /// 移除Tips
        /// </summary>
        /// <param name="text"></param>
        public void RemoveTips(string text)
        {
            if (this.tipsElements.Remove(text, out NodeTipsElement nodeTipsElement) == false) return;
            nodeTipsElement.RemoveFromHierarchy();
        }

        /// <summary>
        /// 清空Tips
        /// </summary>
        public void ClearTips()
        {
            foreach (NodeTipsElement tipsElement in tipsElements.Values) tipsElement.RemoveFromHierarchy();
            tipsElements.Clear();
        }

        /// <summary>
        /// 设置聚焦
        /// </summary>
        public void SetFocus(Color borderColor, long timeMs = -1)
        {
            schedule.Execute(OnSetFocus).ExecuteLater(1);

            void OnSetFocus()
            {
                if (focusCoroutine != null) EditorCoroutineUtility.StopCoroutine(focusCoroutine);

                focusBorder.AddToClassList("enable");

                focusBorder.style.borderTopColor = borderColor;
                focusBorder.style.borderRightColor = borderColor;
                focusBorder.style.borderBottomColor = borderColor;
                focusBorder.style.borderLeftColor = borderColor;

                if (timeMs > 0) focusCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(OnFadeAway(timeMs));
            }
        }

        protected IEnumerator OnFadeAway(long timeMs)
        {
            float time = timeMs / 1000f;
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < time)
            {
                float t = (Time.realtimeSinceStartup - startTime) / time;
                float alpha = 1 - t;

                SetFocusBorderAlpha(alpha);

                yield return 0;
            }

            void SetFocusBorderAlpha(float alpha)
            {
                Color borderTopColor = focusBorder.style.borderTopColor.value;
                borderTopColor.a = alpha;
                focusBorder.style.borderTopColor = borderTopColor;

                Color borderRightColor = focusBorder.style.borderRightColor.value;
                borderRightColor.a = alpha;
                focusBorder.style.borderRightColor = borderRightColor;

                Color borderBottomColor = focusBorder.style.borderBottomColor.value;
                borderBottomColor.a = alpha;
                focusBorder.style.borderBottomColor = borderBottomColor;

                Color borderLeftColor = focusBorder.style.borderLeftColor.value;
                borderLeftColor.a = alpha;
                focusBorder.style.borderLeftColor = borderLeftColor;
            }

            focusCoroutine = null;
            ClearFocus();
        }

        /// <summary>
        /// 清理聚焦
        /// </summary>
        public void ClearFocus()
        {
            schedule.Execute(OnClearFocus).ExecuteLater(1);

            void OnClearFocus()
            {
                if (focusCoroutine != null) EditorCoroutineUtility.StopCoroutine(focusCoroutine);
                focusBorder.RemoveFromClassList("enable");
            }
        }

        /// <summary>
        /// 设置禁用
        /// </summary>
        public void SetDisabled(long timeMs = -1)
        {
            schedule.Execute(OnSetDim).ExecuteLater(1);

            void OnSetDim()
            {
                AddToClassList("disabled");
                this.pickingMode = PickingMode.Ignore;
                this.style.opacity = 0.5f;
                if (timeMs > 0) schedule.Execute(ClearDisabled).ExecuteLater(timeMs);
            }
        }

        /// <summary>
        /// 清除禁用
        /// </summary>
        public void ClearDisabled()
        {
            schedule.Execute(OnClearDim).ExecuteLater(1);

            void OnClearDim()
            {
                RemoveFromClassList("disabled");
                this.pickingMode = PickingMode.Position;
                this.style.opacity = 1.0f;
            }
        }
    }
}