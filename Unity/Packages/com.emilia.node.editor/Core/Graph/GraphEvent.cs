using System;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Graph事件
    /// </summary>
    public interface IGraphEvent : IDisposable
    {
        EditorGraphView graphView { get; set; }
        EventBase eventTarget { get; }
    }

    /// <summary>
    /// Graph事件（泛型实现）
    /// </summary>
    public class GraphEvent<T> : EventBase<T>, IGraphEvent where T : GraphEvent<T>, new()
    {
        protected EditorGraphView _graphView;

        public EditorGraphView graphView
        {
            get => this._graphView;
            set
            {
                this._graphView = value;
                target = value;
            }
        }

        public EventBase eventTarget => this;
    }
}