using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Attributes;
using Emilia.Node.Editor;
using MG.MDV;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 便利贴（支持MarkDown格式）节点表现元素
    /// </summary>
    [EditorItem(typeof(StickyNoteProAsset))]
    public class StickyNoteProView : GraphElement, IEditorItemView, IResizable
    {
        protected StickyNoteProAsset stickyAsset;

        public EditorItemAsset asset => stickyAsset;
        public GraphElement element => this;
        public EditorGraphView graphView { get; protected set; }

        public bool isSelected { get; protected set; }

        protected GUISkin skinLight;
        protected GUISkin skinDark;
        protected string rootPath;

        protected MarkdownViewer markdownViewer;

        protected IMGUIContainer markdownContainer;

        protected VisualElement selectionBorder;

        public StickyNoteProView()
        {
            name = nameof(StickyNoteProView);
        }

        public void Initialize(EditorGraphView graphView, EditorItemAsset asset)
        {
            this.graphView = graphView;
            this.stickyAsset = asset as StickyNoteProAsset;

            capabilities = Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Ascendable | Capabilities.Copiable;

            StyleSheet styleSheet = ResourceUtility.LoadResource<StyleSheet>("Node/Styles/UniversalEditorItemView.uss");
            styleSheets.Add(styleSheet);

            markdownContainer = new IMGUIContainer(OnMarkdownGUI);
            markdownContainer.name = $"{nameof(StickyNoteProView)}-MarkdownContainer";

            markdownContainer.transform.scale = Vector3.one * 0.5f;

            Add(this.markdownContainer);

            selectionBorder = new VisualElement();
            this.selectionBorder.name = "selection-border";

            ResizableElement resizableElement = new();
            Add(resizableElement);

            skinLight = ResourceUtility.LoadResource<GUISkin>("Node/GUISkin/MarkdownSkinLight.guiskin");
            skinDark = ResourceUtility.LoadResource<GUISkin>("Node/GUISkin/MarkdownSkinDark.guiskin");

            rootPath = AssetDatabase.GetAssetPath(graphView.graphAsset);
            markdownViewer = new MarkdownViewer(Preferences.DarkSkin ? skinDark : skinLight, rootPath, this.stickyAsset.context, () => markdownContainer.layout.width);
            markdownViewer.displayRawButton = false;

            graphView.onUpdate -= Update;
            graphView.onUpdate += Update;

            SetPositionNoUndo(stickyAsset.position);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChangedEvent);
            RegisterCallback<MouseDownEvent>(OnMouseDown);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt) { }

        private void OnGeometryChangedEvent(GeometryChangedEvent evt)
        {
            if (markdownContainer == null) return;
            markdownContainer.style.width = layout.width * 2;
            markdownContainer.style.height = layout.height * 2;

            markdownContainer.style.left = -layout.width / 2f;
            markdownContainer.style.top = -layout.height / 2f;
        }

        protected virtual void OnMouseDown(MouseDownEvent evt)
        {
            graphView?.UpdateSelected();
        }
        
        private void OnMarkdownGUI()
        {
            Event evt = Event.current;
            if (evt.type == EventType.ValidateCommand)
            {
                ValidateCommandEvent validateCommandEvent = ValidateCommandEvent.GetPooled(evt.commandName);
                validateCommandEvent.target = graphView;

                graphView.OnValidateCommand_Internals(validateCommandEvent);

                validateCommandEvent.Dispose();
                evt.Use();
            }

            if (evt.type == EventType.ExecuteCommand)
            {
                ExecuteCommandEvent executeCommandEvent = ExecuteCommandEvent.GetPooled(evt.commandName);
                executeCommandEvent.target = graphView;

                graphView.OnExecuteCommand_Internals(executeCommandEvent);
                executeCommandEvent.Dispose();

                evt.Use();
            }

            if (markdownViewer == null) return;
            markdownViewer.Draw();
        }

        void Update()
        {
            markdownViewer?.Update();
            markdownContainer?.MarkDirtyRepaint();
        }

        public void Delete()
        {
            this.graphView.itemSystem.DeleteItem(this);
        }

        public void RemoveView()
        {
            graphView.RemoveItemView(this);
        }

        public ICopyPastePack GetPack() => new ItemCopyPastePack(asset);

        public bool Validate() => true;

        public bool IsSelected() => isSelected;

        public void Select()
        {
            isSelected = true;
            AddToClassList("selected");
        }

        public void Unselect()
        {
            isSelected = false;
            RemoveFromClassList("selected");
        }

        public IEnumerable<Object> GetSelectedObjects()
        {
            yield return asset;
        }

        public override void SetPosition(Rect rect)
        {
            base.SetPosition(rect);
            if (this.graphView == null) return;
            this.graphView.RegisterCompleteObjectUndo("Graph MoveNode");
            stickyAsset.position = rect;
        }

        public void SetPositionNoUndo(Rect newPos)
        {
            base.SetPosition(newPos);
            asset.position = newPos;
        }

        public void OnValueChanged(bool isSilent = false)
        {
            markdownViewer?.ResetContent(stickyAsset.context);

            SetPositionNoUndo(stickyAsset.position);
            if (isSilent == false) graphView.graphSave.SetDirty();
        }

        public void OnStartResize() { }

        public void OnResized()
        {
            SetPosition(GetPosition());
        }

        public void Dispose()
        {
            graphView.onUpdate -= Update;
            graphView.onUpdate -= Update;
        }
    }
}