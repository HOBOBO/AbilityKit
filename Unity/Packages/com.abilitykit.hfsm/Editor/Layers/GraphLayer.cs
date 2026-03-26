using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Base class for all graph layers.
    /// Each layer handles a specific aspect of rendering and interaction.
    /// </summary>
    public abstract class GraphLayer
    {
        protected HfsmEditorContext Context => _context;
        protected EditorWindow EditorWindow => _editorWindow;

        private HfsmEditorContext _context;
        private readonly EditorWindow _editorWindow;

        /// <summary>
        /// The visible area of the graph in content coordinates.
        /// </summary>
        protected Rect ContentBounds { get; private set; }

        /// <summary>
        /// The visible area of the graph in screen coordinates.
        /// </summary>
        internal Rect ViewBounds { get; set; }

        /// <summary>
        /// Transform from content to screen coordinates.
        /// </summary>
        protected Matrix4x4 ContentToScreenMatrix
        {
            get
            {
                // screen = content * zoom + panOffset
                // Using TRS where translation = panOffset, scale = zoom
                return Matrix4x4.TRS(
                    Context.PanOffset,
                    Quaternion.identity,
                    new Vector3(Context.ZoomFactor, Context.ZoomFactor, 1)
                );
            }
        }

        /// <summary>
        /// Transform from screen to content coordinates.
        /// </summary>
        protected Matrix4x4 ScreenToContentMatrix => ContentToScreenMatrix.inverse;

        protected GraphLayer(EditorWindow editorWindow)
        {
            _editorWindow = editorWindow;
        }

        /// <summary>
        /// Called when the layer is set up with a context.
        /// </summary>
        public virtual void Initialize(HfsmEditorContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Called to render the layer.
        /// </summary>
        public virtual void OnGUI(Rect rect)
        {
            UpdateBounds(rect);
        }

        /// <summary>
        /// Called to process input events.
        /// </summary>
        public virtual void ProcessEvent()
        {
        }

        /// <summary>
        /// Called every frame for updates.
        /// </summary>
        public virtual void Update()
        {
        }

        /// <summary>
        /// Updates the view bounds.
        /// </summary>
        protected void UpdateBounds(Rect rect)
        {
            ViewBounds = rect;

            // Calculate content bounds from view bounds
            Vector2 contentMin = ScreenToContentMatrix.MultiplyPoint3x4(rect.position);
            Vector2 contentMax = ScreenToContentMatrix.MultiplyPoint3x4(new Vector2(rect.xMax, rect.yMax));
            ContentBounds = Rect.MinMaxRect(contentMin.x, contentMin.y, contentMax.x, contentMax.y);
        }

        /// <summary>
        /// Converts a screen position to content position.
        /// </summary>
        protected Vector2 ScreenPosToContent(Vector2 screenPosition)
        {
            return ScreenToContentMatrix.MultiplyPoint3x4(screenPosition);
        }

        /// <summary>
        /// Converts a content position to screen position.
        /// </summary>
        protected Vector2 ContentPosToScreen(Vector2 contentPosition)
        {
            return ContentToScreenMatrix.MultiplyPoint3x4(contentPosition);
        }

        /// <summary>
        /// Gets the screen rect from a content rect.
        /// </summary>
        protected Rect ContentRectToScreen(Rect contentRect)
        {
            Vector2 min = ContentPosToScreen(contentRect.position);
            Vector2 max = ContentPosToScreen(new Vector2(contentRect.xMax, contentRect.yMax));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// Checks if a screen position is within a content rect.
        /// </summary>
        protected bool ScreenPointInContentRect(Vector2 screenPosition, Rect contentRect)
        {
            Vector2 contentPos = ScreenPosToContent(screenPosition);
            return contentRect.Contains(contentPos);
        }
    }
}
