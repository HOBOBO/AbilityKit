using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Node.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 布局工具类
    /// </summary>
    public static class GraphLayoutUtility
    {
        /// <summary>
        /// 方向
        /// </summary>
        [Flags]
        public enum AlignmentType
        {
            /// <summary>
            /// 横向
            /// </summary>
            Horizontal = 1,

            /// <summary>
            /// 垂直
            /// </summary>
            Vertical = 2,

            /// <summary>
            /// 位置-顶部/左边
            /// </summary>
            TopOrLeft = 4,

            /// <summary>
            /// 位置-中心
            /// </summary>
            Center = 8,

            /// <summary>
            /// 位置-底部/右边
            /// </summary>
            BottomOrRight = 16,
        }

        /// <summary>
        /// 开始布局
        /// </summary>
        public static void Start(float interval, AlignmentType alignmentType, List<IEditorNodeView> elements)
        {
            int count = elements.Count;
            if (count == 0) return;

            Undo.IncrementCurrentGroup();

            if (elements.Count == 1)
            {
                IEditorNodeView element = elements.FirstOrDefault();

                List<IEditorNodeView> inputs = element.GetInputNodeViews();
                if (inputs.Count > 0)
                {
                    IEditorNodeView closestInput = inputs.OrderBy(input => Vector2.Distance(element.asset.position.position, input.asset.position.position)).FirstOrDefault();
                    if (closestInput != null && elements.Contains(closestInput) == false) elements.Add(closestInput);
                }

                List<IEditorNodeView> outputs = element.GetOutputNodeViews();
                if (outputs.Count > 0)
                {
                    IEditorNodeView closestOutput = outputs.OrderBy(output => Vector2.Distance(element.asset.position.position, output.asset.position.position)).FirstOrDefault();
                    if (closestOutput != null && elements.Contains(closestOutput) == false) elements.Add(closestOutput);
                }
            }

            Dictionary<IEditorNodeView, Vector2> nodeLayoutMovePosition = null;

            if (alignmentType.HasFlag(AlignmentType.Horizontal)) nodeLayoutMovePosition = LayoutHorizontal(interval, alignmentType, elements);
            else if (alignmentType.HasFlag(AlignmentType.Vertical)) nodeLayoutMovePosition = LayoutVertical(interval, alignmentType, elements);

            RestoreNode(nodeLayoutMovePosition);

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Undo.IncrementCurrentGroup();
        }

        private static Dictionary<IEditorNodeView, Vector2> LayoutHorizontal(float interval, AlignmentType alignmentType, List<IEditorNodeView> elements)
        {
            Dictionary<IEditorNodeView, Vector2> nodeLayoutMovePosition = new();

            float y = GetY(alignmentType, elements);

            elements.Sort((a, b) => a.asset.position.x.CompareTo(b.asset.position.x));

            float startX = elements.FirstOrDefault().asset.position.x;
            float currentX = startX;

            for (int i = 0; i < elements.Count; i++)
            {
                IEditorNodeView element = elements[i];
                Rect position = element.asset.position;

                position.y = y;
                float width = element.asset.position.width;

                position.x = currentX;
                currentX += width + interval;

                Vector3 relativePosition = position.position - element.asset.position.position;
                nodeLayoutMovePosition[element] = relativePosition;

                element.SetPosition(position);
            }

            return nodeLayoutMovePosition;
        }

        private static Dictionary<IEditorNodeView, Vector2> LayoutVertical(float interval, AlignmentType alignmentType, List<IEditorNodeView> elements)
        {
            Dictionary<IEditorNodeView, Vector2> nodeLayoutMovePosition = new();

            float x = GetX(alignmentType, elements);

            elements.Sort((a, b) => a.asset.position.y.CompareTo(b.asset.position.y));

            float startY = elements.FirstOrDefault().asset.position.y;

            for (int i = 0; i < elements.Count; i++)
            {
                IEditorNodeView element = elements[i];
                Rect position = element.asset.position;

                position.x = x;
                position.y = startY + interval * i;

                Vector3 relativePosition = position.position - element.asset.position.position;
                nodeLayoutMovePosition[element] = relativePosition;

                element.SetPosition(position);
            }

            return nodeLayoutMovePosition;
        }

        private static float GetY(AlignmentType alignmentType, List<IEditorNodeView> elements)
        {
            if (alignmentType.HasFlag(AlignmentType.TopOrLeft)) return elements.Min(e => e.asset.position.y);
            if (alignmentType.HasFlag(AlignmentType.Center)) return elements.Average(e => e.asset.position.y);
            if (alignmentType.HasFlag(AlignmentType.BottomOrRight)) return elements.Max(e => e.asset.position.y);
            return 0;
        }

        private static float GetX(AlignmentType alignmentType, List<IEditorNodeView> elements)
        {
            if (alignmentType.HasFlag(AlignmentType.TopOrLeft)) return elements.Min(e => e.asset.position.x);
            if (alignmentType.HasFlag(AlignmentType.Center)) return elements.Average(e => e.asset.position.x);
            if (alignmentType.HasFlag(AlignmentType.BottomOrRight)) return elements.Max(e => e.asset.position.x);
            return 0;
        }

        private static void RestoreNode(Dictionary<IEditorNodeView, Vector2> nodeLayoutMovePosition)
        {
            Queue<IEditorNodeView> queue = new(nodeLayoutMovePosition.Keys);

            while (queue.Count > 0)
            {
                IEditorNodeView node = queue.Dequeue();
                Vector2 relativePosition = nodeLayoutMovePosition[node];
                if (relativePosition == Vector2.zero) continue;

                HashSet<IEditorNodeView> connectNodes = new();
                connectNodes.AddRange(node.GetInputNodeViews());
                connectNodes.AddRange(node.GetOutputNodeViews());

                foreach (IEditorNodeView connected in connectNodes)
                {
                    if (nodeLayoutMovePosition.ContainsKey(connected)) continue;

                    Rect position = connected.asset.position;
                    position.x += relativePosition.x;
                    position.y += relativePosition.y;

                    Vector2 newRelativePosition = position.position - connected.asset.position.position;
                    nodeLayoutMovePosition[connected] = newRelativePosition;
                    connected.SetPosition(position);

                    queue.Enqueue(connected);
                }
            }
        }
    }
}