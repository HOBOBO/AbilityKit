#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Samples.Abstractions;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Samples.Editor
{
    /// <summary>
    /// 用最基础的 IMGUI 呈现示例的语义可视化数据。不做画布级复刻（见设计文档 §3.2 / §3.3）。
    ///
    /// 刻意不使用 <c>Handles</c>：连线画成正交折线（若干 <see cref="EditorGUI.DrawRect"/> 细条），
    /// 箭头用三层递减宽度的矩形拼出。少一个 API 面，就少一处只能在 Unity 里才发现的错误。
    ///
    /// 数据覆盖决定了两块内容的地位不同：
    ///   * <c>visualFrames</c> —— 37/37 都有，是主线；
    ///   * <c>visualModel.nodes/edges/metrics</c> —— 仅 15/37 有，存在时才画。
    /// 没有帧、也没有节点图的样例会得到一句明确的说明，而不是一片空白。
    /// </summary>
    internal static class SampleVisualRenderer
    {
        private const float NodeWidth = 148f;
        private const float NodeHeight = 46f;
        private const float LayerGap = 64f;
        private const float RowGap = 18f;
        private const float CanvasPadding = 10f;
        private const float MaxCanvasHeight = 420f;

        // --- 帧序列（主线）-----------------------------------------------------

        /// <summary>
        /// 画帧推进控件，返回被夹紧后的帧号。
        /// </summary>
        internal static int DrawFrameStepper(SampleCatalogEntry entry, int frameIndex)
        {
            var frames = entry.VisualFrames;
            if (frames.Count == 0)
            {
                return 0;
            }

            frameIndex = Mathf.Clamp(frameIndex, 0, frames.Count - 1);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("<", EditorStyles.miniButtonLeft, GUILayout.Width(26f)))
            {
                frameIndex--;
            }

            frameIndex = EditorGUILayout.IntSlider(frameIndex, 0, frames.Count - 1);

            if (GUILayout.Button(">", EditorStyles.miniButtonRight, GUILayout.Width(26f)))
            {
                frameIndex++;
            }

            EditorGUILayout.LabelField(
                (frameIndex + 1) + " / " + frames.Count,
                EditorStyles.miniLabel,
                GUILayout.Width(52f));
            EditorGUILayout.EndHorizontal();

            return Mathf.Clamp(frameIndex, 0, frames.Count - 1);
        }

        /// <summary>
        /// 画当前帧的细节：标题、所属步骤、说明、状态变化与高亮。
        /// </summary>
        internal static void DrawFrameDetail(SampleCatalogEntry entry, int frameIndex)
        {
            var frames = entry.VisualFrames;
            if (frames.Count == 0 || frameIndex < 0 || frameIndex >= frames.Count)
            {
                return;
            }

            var frame = frames[frameIndex];

            if (!string.IsNullOrWhiteSpace(frame.Title))
            {
                EditorGUILayout.LabelField(frame.Title, EditorStyles.boldLabel);
            }

            if (!string.IsNullOrWhiteSpace(frame.VisualStep))
            {
                EditorGUILayout.LabelField("步骤：" + frame.VisualStep, EditorStyles.miniLabel);
            }

            if (!string.IsNullOrWhiteSpace(frame.Description))
            {
                EditorGUILayout.LabelField(frame.Description, EditorStyles.wordWrappedLabel);
            }

            if (!string.IsNullOrWhiteSpace(frame.OutputHint))
            {
                EditorGUILayout.LabelField("对应输出：" + frame.OutputHint, EditorStyles.miniLabel);
            }

            DrawBulletList("状态变化", frame.StateChanges);
            DrawBulletList("高亮", frame.Highlights);
        }

        /// <summary>
        /// 把 guide.visualSteps 画成一行步骤，当前帧落在哪一步就高亮哪一步。
        /// </summary>
        internal static void DrawStepProgress(SampleCatalogEntry entry, int frameIndex)
        {
            var steps = entry.Guide.VisualSteps;
            if (steps.Length == 0)
            {
                return;
            }

            var current = frameIndex >= 0 && frameIndex < entry.VisualFrames.Count
                ? entry.VisualFrames[frameIndex].VisualStep
                : string.Empty;

            EditorGUILayout.LabelField("步骤", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                var isCurrent = !string.IsNullOrEmpty(current) &&
                                string.Equals(step, current, StringComparison.OrdinalIgnoreCase);
                var previous = GUI.color;
                if (isCurrent)
                {
                    GUI.color = EditorGUIUtility.isProSkin
                        ? new Color(0.45f, 0.75f, 1f)
                        : new Color(0.1f, 0.4f, 0.8f);
                }

                EditorGUILayout.LabelField(
                    isCurrent ? "[" + step + "]" : step,
                    EditorStyles.miniLabel,
                    GUILayout.Width(Mathf.Max(48f, step.Length * 8f + 16f)));
                GUI.color = previous;

                if (i < steps.Length - 1)
                {
                    EditorGUILayout.LabelField(">", EditorStyles.miniLabel, GUILayout.Width(10f));
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // --- 语义图（15/37 有数据）---------------------------------------------

        /// <summary>
        /// 有节点时才画。返回是否画了东西。
        /// </summary>
        internal static bool DrawGraph(SampleVisualModel model)
        {
            var nodes = model.Nodes;
            if (nodes == null || nodes.Length == 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(model.Title))
            {
                EditorGUILayout.LabelField(model.Title, EditorStyles.boldLabel);
            }

            if (!string.IsNullOrWhiteSpace(model.Description))
            {
                EditorGUILayout.LabelField(model.Description, EditorStyles.wordWrappedLabel);
            }

            var layout = LayeredLayout.Build(nodes, model.Edges);
            var width = layout.LayerCount * (NodeWidth + LayerGap) + CanvasPadding * 2f;
            var height = layout.MaxRows * (NodeHeight + RowGap) + CanvasPadding * 2f;
            height = Mathf.Min(height, MaxCanvasHeight);

            // 图比可用宽度大时留出横向滚动的余地，而不是把节点压扁。
            var canvas = GUILayoutUtility.GetRect(
                Mathf.Max(width, 100f),
                height,
                GUILayout.ExpandWidth(true));

            GUI.BeginClip(canvas);
            var origin = new Vector2(canvas.x + CanvasPadding, canvas.y + CanvasPadding);
            DrawEdges(model.Edges, layout, origin);
            DrawNodes(nodes, layout, origin);
            GUI.EndClip();

            return true;
        }

        private static void DrawNodes(
            SampleVisualNode[] nodes,
            LayeredLayout layout,
            Vector2 origin)
        {
            foreach (var node in nodes)
            {
                if (!layout.Positions.TryGetValue(node.Id, out var slot))
                {
                    continue;
                }

                var rect = new Rect(
                    origin.x + slot.x,
                    origin.y + slot.y,
                    NodeWidth,
                    NodeHeight);

                GUI.Box(rect, GUIContent.none, GUI.skin.box);

                var labelRect = new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f);
                GUI.Label(labelRect, node.Label, EditorStyles.boldLabel);

                var subtitle = string.IsNullOrWhiteSpace(node.Kind)
                    ? node.Description
                    : node.Kind + (string.IsNullOrWhiteSpace(node.Description) ? string.Empty : " · " + node.Description);
                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    var subRect = new Rect(rect.x + 6f, rect.y + 22f, rect.width - 12f, rect.height - 24f);
                    GUI.Label(subRect, subtitle, EditorStyles.miniLabel);
                }
            }
        }

        private static void DrawEdges(
            SampleVisualEdge[] edges,
            LayeredLayout layout,
            Vector2 origin)
        {
            if (edges == null)
            {
                return;
            }

            var lineColor = EditorGUIUtility.isProSkin
                ? new Color(0.7f, 0.7f, 0.7f, 0.8f)
                : new Color(0.25f, 0.25f, 0.25f, 0.8f);

            foreach (var edge in edges)
            {
                if (!layout.Positions.TryGetValue(edge.From, out var from) ||
                    !layout.Positions.TryGetValue(edge.To, out var to))
                {
                    continue;
                }

                var start = new Vector2(origin.x + from.x + NodeWidth, origin.y + from.y + NodeHeight * 0.5f);
                var end = new Vector2(origin.x + to.x, origin.y + to.y + NodeHeight * 0.5f);
                var midX = Mathf.Lerp(start.x, end.x, 0.5f);

                // 正交折线：横 → 竖 → 横。
                DrawLine(new Vector2(start.x, start.y), new Vector2(midX, start.y), lineColor);
                DrawLine(new Vector2(midX, start.y), new Vector2(midX, end.y), lineColor);
                DrawLine(new Vector2(midX, end.y), new Vector2(end.x - 7f, end.y), lineColor);
                DrawArrowHead(end, lineColor);

                if (!string.IsNullOrWhiteSpace(edge.Label))
                {
                    var labelRect = new Rect(midX - 40f, Mathf.Min(start.y, end.y) - 16f, 80f, 16f);
                    GUI.Label(labelRect, edge.Label, EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            var x = Mathf.Min(a.x, b.x);
            var y = Mathf.Min(a.y, b.y);
            var width = Mathf.Max(1f, Mathf.Abs(a.x - b.x));
            var height = Mathf.Max(1f, Mathf.Abs(a.y - b.y));
            EditorGUI.DrawRect(new Rect(x, y, width, height), color);
        }

        /// <summary>
        /// 箭头用逐层收窄的矩形拼出，避免依赖 GUIStyle 里的三角字形。
        /// </summary>
        private static void DrawArrowHead(Vector2 tip, Color color)
        {
            const int rows = 4;
            for (var i = 0; i < rows; i++)
            {
                var height = 1f;
                var y = tip.y - (rows - 1) * 0.5f + i;
                var width = rows - i;
                EditorGUI.DrawRect(new Rect(tip.x - width, y, width, height), color);
            }
        }

        // --- 指标表（15/37 有数据）---------------------------------------------

        internal static void DrawMetrics(SampleVisualModel model)
        {
            var metrics = model.Metrics;
            if (metrics == null || metrics.Length == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("指标", EditorStyles.boldLabel);
            foreach (var metric in metrics)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    string.IsNullOrWhiteSpace(metric.Label) ? metric.Key : metric.Label,
                    GUILayout.Width(180f));
                EditorGUILayout.LabelField(metric.Value, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrWhiteSpace(metric.Kind))
                {
                    EditorGUILayout.LabelField(metric.Kind, EditorStyles.miniLabel, GUILayout.Width(70f));
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// 既没有帧也没有节点图时的说明——避免读者以为界面坏了。
        /// </summary>
        internal static void DrawUnavailableNotice()
        {
            EditorGUILayout.HelpBox(
                "这条示例既没有 visualFrames 也没有 nodes，暂无语义图可画。",
                MessageType.None);
        }

        private static void DrawBulletList(string title, IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (var i = 0; i < items.Count; i++)
            {
                EditorGUILayout.LabelField("•  " + items[i], EditorStyles.wordWrappedLabel);
            }
        }

        // --- 分层布局 ----------------------------------------------------------

        /// <summary>
        /// 纯拓扑分层：层号 = 从入度为 0 的节点出发的最长路径长度。
        ///
        /// 刻意不按 node.kind 分层——实测 29 个节点的 kind 为空，其余取值也杂
        /// （output/node/phase/api/...），按它分层会得到一张读不懂的图。
        /// </summary>
        private sealed class LayeredLayout
        {
            internal readonly Dictionary<string, Vector2> Positions =
                new Dictionary<string, Vector2>(StringComparer.Ordinal);

            internal int LayerCount { get; private set; }

            internal int MaxRows { get; private set; }

            internal static LayeredLayout Build(
                SampleVisualNode[] nodes,
                SampleVisualEdge[] edges)
            {
                var layout = new LayeredLayout();
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < nodes.Length; i++)
                {
                    index[nodes[i].Id] = i;
                }

                var predecessors = new List<int>[nodes.Length];
                for (var i = 0; i < nodes.Length; i++)
                {
                    predecessors[i] = new List<int>();
                }

                if (edges != null)
                {
                    foreach (var edge in edges)
                    {
                        if (index.TryGetValue(edge.From, out var from) &&
                            index.TryGetValue(edge.To, out var to) &&
                            from != to)
                        {
                            predecessors[to].Add(from);
                        }
                    }
                }

                var layer = new int[nodes.Length];
                // 最长路径；用 nodes.Length 作为迭代上限，环上自然停住而不是死循环。
                for (var pass = 0; pass < nodes.Length; pass++)
                {
                    var changed = false;
                    for (var i = 0; i < nodes.Length; i++)
                    {
                        foreach (var p in predecessors[i])
                        {
                            if (layer[i] < layer[p] + 1)
                            {
                                layer[i] = layer[p] + 1;
                                changed = true;
                            }
                        }
                    }

                    if (!changed)
                    {
                        break;
                    }
                }

                var rows = new int[nodes.Length];
                var rowsPerLayer = new Dictionary<int, int>();
                for (var i = 0; i < nodes.Length; i++)
                {
                    var l = layer[i];
                    rowsPerLayer.TryGetValue(l, out var used);
                    rows[i] = used;
                    rowsPerLayer[l] = used + 1;
                    layout.LayerCount = Mathf.Max(layout.LayerCount, l + 1);
                    layout.MaxRows = Mathf.Max(layout.MaxRows, used + 1);
                }

                for (var i = 0; i < nodes.Length; i++)
                {
                    var l = layer[i];
                    // 每层的行数不同，居中对齐看起来更稳。
                    var offset = (layout.MaxRows - rowsPerLayer[l]) * (NodeHeight + RowGap) * 0.5f;
                    layout.Positions[nodes[i].Id] = new Vector2(
                        l * (NodeWidth + LayerGap),
                        offset + rows[i] * (NodeHeight + RowGap));
                }

                return layout;
            }
        }
    }
}
