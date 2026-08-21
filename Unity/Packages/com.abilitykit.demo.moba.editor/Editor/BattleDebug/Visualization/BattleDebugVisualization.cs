using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal readonly struct BattleDebugLegendItem
    {
        public BattleDebugLegendItem(string label, Color color)
        {
            Label = label ?? string.Empty;
            Color = color;
        }

        public string Label { get; }
        public Color Color { get; }
    }

    internal static class BattleDebugLegend
    {
        public static void Draw(IReadOnlyList<BattleDebugLegendItem> items)
        {
            if (items == null || items.Count == 0) return;

            var rect = EditorGUILayout.GetControlRect(false, 18f);
            var x = rect.x;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var labelContent = new GUIContent(item.Label);
                var labelWidth = EditorStyles.miniLabel.CalcSize(labelContent).x;
                if (x + 12f + labelWidth > rect.xMax) break;

                EditorGUI.DrawRect(
                    new Rect(x, rect.y + 5f, 8f, 8f),
                    item.Color);
                GUI.Label(
                    new Rect(x + 12f, rect.y, labelWidth, rect.height),
                    labelContent,
                    EditorStyles.miniLabel);
                x += 20f + labelWidth;
            }
        }
    }

    internal readonly struct BattleDebugHistogramSample
    {
        public BattleDebugHistogramSample(int frame, int seriesIndex)
        {
            Frame = frame;
            SeriesIndex = seriesIndex;
        }

        public int Frame { get; }
        public int SeriesIndex { get; }
    }

    internal sealed class BattleDebugHistogramSeries
    {
        public const int MaximumBinCount = 64;

        public BattleDebugHistogramSeries(string label, Color color)
        {
            Label = label ?? string.Empty;
            Color = color;
            Counts = new int[MaximumBinCount];
        }

        public string Label { get; }
        public Color Color { get; }
        public int[] Counts { get; }
    }

    internal enum BattleDebugTimelineInteractionKind
    {
        None = 0,
        SelectFrame = 1,
        SelectRange = 2,
        Zoom = 3,
        Pan = 4
    }

    internal readonly struct BattleDebugTimelineInteractionResult
    {
        public BattleDebugTimelineInteractionResult(
            BattleDebugTimelineInteractionKind kind,
            int frame,
            BattleDiagnosticFrameRange range)
        {
            Kind = kind;
            Frame = frame;
            Range = range;
        }

        public BattleDebugTimelineInteractionKind Kind { get; }
        public int Frame { get; }
        public BattleDiagnosticFrameRange Range { get; }
        public bool HasValue => Kind != BattleDebugTimelineInteractionKind.None;
    }

    internal readonly struct BattleDebugTimelineOverviewItem
    {
        public BattleDebugTimelineOverviewItem(int startFrame, int endFrame)
        {
            StartFrame = Math.Min(startFrame, endFrame);
            EndFrame = Math.Max(startFrame, endFrame);
        }

        public int StartFrame { get; }
        public int EndFrame { get; }
    }

    internal sealed class BattleDebugTimelineOverviewBuffer
    {
        public const int MaximumBinCount = 128;

        public BattleDebugTimelineOverviewBuffer()
        {
            Counts = new int[MaximumBinCount];
        }

        public int[] Counts { get; }
    }

    internal static class BattleDebugTimelineInteraction
    {
        private const int ControlHint = 0x42544454;
        private const float DragThreshold = 3f;

        private enum DragMode
        {
            None = 0,
            SelectRange = 1,
            Pan = 2
        }

        private static int _activeControlId;
        private static DragMode _dragMode;
        private static float _dragStartX;
        private static float _dragCurrentX;
        private static BattleDiagnosticFrameRange _dragRange;

        public static BattleDebugTimelineInteractionResult Handle(
            Rect rect,
            BattleDiagnosticFrameRange visibleRange,
            bool primaryDragSelectsRange = true,
            bool showHelpTooltip = true,
            bool enableZoomAndPan = true)
        {
            if (!visibleRange.IsValid || rect.width <= 1f)
            {
                return default;
            }

            var controlId = GUIUtility.GetControlID(ControlHint, FocusType.Passive, rect);
            var currentEvent = Event.current;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Zoom);
            if (showHelpTooltip)
            {
                GUI.Label(
                    rect,
                    new GUIContent(
                        string.Empty,
                    enableZoomAndPan
                        ? "Click to select a frame. Drag to select a range. " +
                          "Use the wheel to zoom; Alt+drag or middle-drag to pan."
                        : "Click to select a frame or drag to select a range."));
            }

            if (_activeControlId == controlId && currentEvent.type == EventType.Repaint)
            {
                DrawDragPreview(rect);
            }

            var eventType = currentEvent.GetTypeForControl(controlId);
            if (enableZoomAndPan &&
                eventType == EventType.ScrollWheel &&
                rect.Contains(currentEvent.mousePosition))
            {
                var anchorFrame = FrameAtPosition(
                    currentEvent.mousePosition.x,
                    rect,
                    visibleRange);
                var scale = Math.Pow(1.12d, currentEvent.delta.y);
                var nextRange = BattleDiagnosticTimeRange.Fixed(
                        visibleRange.FirstFrame,
                        visibleRange.LastFrame)
                    .Zoom(anchorFrame, scale)
                    .Range;
                currentEvent.Use();
                return new BattleDebugTimelineInteractionResult(
                    BattleDebugTimelineInteractionKind.Zoom,
                    anchorFrame,
                    nextRange);
            }

            if (eventType == EventType.MouseDown && rect.Contains(currentEvent.mousePosition))
            {
                var pan = enableZoomAndPan &&
                          (currentEvent.button == 2 ||
                           currentEvent.button == 0 && currentEvent.alt);
                var selectRange = currentEvent.button == 0 &&
                                  (primaryDragSelectsRange || currentEvent.shift);
                if (pan || selectRange)
                {
                    GUIUtility.hotControl = controlId;
                    _activeControlId = controlId;
                    _dragMode = pan ? DragMode.Pan : DragMode.SelectRange;
                    _dragStartX = currentEvent.mousePosition.x;
                    _dragCurrentX = _dragStartX;
                    _dragRange = visibleRange;
                    currentEvent.Use();
                }
            }
            else if (eventType == EventType.MouseDrag && _activeControlId == controlId)
            {
                _dragCurrentX = currentEvent.mousePosition.x;
                GUI.changed = true;
                currentEvent.Use();
            }
            else if (eventType == EventType.MouseUp && _activeControlId == controlId)
            {
                _dragCurrentX = currentEvent.mousePosition.x;
                var result = ResolveDragResult(rect);
                GUIUtility.hotControl = 0;
                _activeControlId = 0;
                _dragMode = DragMode.None;
                currentEvent.Use();
                return result;
            }

            return default;
        }

        public static bool Apply(
            BattleDiagnosticWorkspaceState workspaceState,
            BattleDebugTimelineInteractionResult interaction)
        {
            if (workspaceState == null || !interaction.HasValue)
            {
                return false;
            }

            switch (interaction.Kind)
            {
                case BattleDebugTimelineInteractionKind.SelectFrame:
                    if (!BattleDiagnosticFrames.IsValid(interaction.Frame) ||
                        workspaceState.FrameCursor.Frame == interaction.Frame)
                    {
                        return false;
                    }
                    workspaceState.SetFrame(interaction.Frame);
                    return true;
                case BattleDebugTimelineInteractionKind.SelectRange:
                    return SetRange(
                        workspaceState,
                        interaction.Range,
                        BattleDiagnosticTimeRangeChangeReason.Brushed);
                case BattleDebugTimelineInteractionKind.Zoom:
                    return SetRange(
                        workspaceState,
                        interaction.Range,
                        BattleDiagnosticTimeRangeChangeReason.Zoomed);
                case BattleDebugTimelineInteractionKind.Pan:
                    return SetRange(
                        workspaceState,
                        interaction.Range,
                        BattleDiagnosticTimeRangeChangeReason.Panned);
                default:
                    return false;
            }
        }

        internal static int FrameAtNormalizedPosition(
            BattleDiagnosticFrameRange range,
            double normalizedPosition)
        {
            if (!range.IsValid)
            {
                return BattleDiagnosticFrames.Invalid;
            }

            var t = Math.Max(0d, Math.Min(1d, normalizedPosition));
            var frameCount = (long)range.LastFrame - range.FirstFrame + 1L;
            var offset = Math.Min(
                frameCount - 1L,
                (long)Math.Floor(t * frameCount));
            return range.FirstFrame + (int)offset;
        }

        private static BattleDebugTimelineInteractionResult ResolveDragResult(Rect rect)
        {
            if (_dragMode == DragMode.Pan)
            {
                var frameCount = (long)_dragRange.LastFrame - _dragRange.FirstFrame + 1L;
                var rawFrameDelta = Math.Round(
                    -(_dragCurrentX - _dragStartX) / Math.Max(1f, rect.width) * frameCount);
                var frameDelta = rawFrameDelta <= int.MinValue
                    ? int.MinValue
                    : rawFrameDelta >= int.MaxValue
                        ? int.MaxValue
                        : (int)rawFrameDelta;
                var nextRange = BattleDiagnosticTimeRange.Fixed(
                        _dragRange.FirstFrame,
                        _dragRange.LastFrame)
                    .Pan(frameDelta)
                    .Range;
                return new BattleDebugTimelineInteractionResult(
                    BattleDebugTimelineInteractionKind.Pan,
                    BattleDiagnosticFrames.Invalid,
                    nextRange);
            }

            var firstFrame = FrameAtPosition(_dragStartX, rect, _dragRange);
            var lastFrame = FrameAtPosition(_dragCurrentX, rect, _dragRange);
            if (Math.Abs(_dragCurrentX - _dragStartX) < DragThreshold)
            {
                return new BattleDebugTimelineInteractionResult(
                    BattleDebugTimelineInteractionKind.SelectFrame,
                    lastFrame,
                    default);
            }

            return new BattleDebugTimelineInteractionResult(
                BattleDebugTimelineInteractionKind.SelectRange,
                BattleDiagnosticFrames.Invalid,
                new BattleDiagnosticFrameRange(
                    Math.Min(firstFrame, lastFrame),
                    Math.Max(firstFrame, lastFrame)));
        }

        private static int FrameAtPosition(
            float x,
            Rect rect,
            BattleDiagnosticFrameRange range)
        {
            return FrameAtNormalizedPosition(
                range,
                (x - rect.x) / Math.Max(1f, rect.width));
        }

        private static void DrawDragPreview(Rect rect)
        {
            if (_dragMode == DragMode.SelectRange)
            {
                var firstX = Mathf.Clamp(Mathf.Min(_dragStartX, _dragCurrentX), rect.x, rect.xMax);
                var lastX = Mathf.Clamp(Mathf.Max(_dragStartX, _dragCurrentX), rect.x, rect.xMax);
                EditorGUI.DrawRect(
                    new Rect(firstX, rect.y, Mathf.Max(1f, lastX - firstX), rect.height),
                    new Color(0.22f, 0.55f, 0.92f, 0.25f));
                return;
            }

            if (_dragMode == DragMode.Pan)
            {
                EditorGUI.DrawRect(rect, new Color(1f, 0.82f, 0.22f, 0.08f));
            }
        }

        private static bool SetRange(
            BattleDiagnosticWorkspaceState workspaceState,
            BattleDiagnosticFrameRange range,
            BattleDiagnosticTimeRangeChangeReason changeReason)
        {
            return range.IsValid && workspaceState.SetTimeRange(
                range.FirstFrame,
                range.LastFrame,
                changeReason);
        }
    }

    internal static class BattleDebugTimelineOverview
    {
        public static BattleDebugTimelineInteractionResult Draw(
            IReadOnlyList<BattleDebugTimelineOverviewItem> items,
            BattleDiagnosticFrameRange loadedRange,
            BattleDiagnosticFrameRange visibleRange,
            int cursorFrame,
            BattleDebugTimelineOverviewBuffer buffer)
        {
            if (!loadedRange.IsValid || buffer == null)
            {
                return default;
            }

            var controlRect = EditorGUILayout.GetControlRect(false, 54f);
            var plotRect = new Rect(
                controlRect.x + 2f,
                controlRect.y + 2f,
                Mathf.Max(1f, controlRect.width - 4f),
                32f);
            EditorGUI.DrawRect(plotRect, new Color(0f, 0f, 0f, 0.16f));

            var binCount = Mathf.Clamp(
                Mathf.FloorToInt(plotRect.width / 5f),
                16,
                BattleDebugTimelineOverviewBuffer.MaximumBinCount);
            ProjectDensity(items, loadedRange, buffer.Counts, binCount, out var peak);
            DrawDensity(plotRect, buffer.Counts, binCount, peak);
            DrawVisibleWindow(plotRect, loadedRange, visibleRange);
            DrawCursor(plotRect, loadedRange, cursorFrame);

            var intersection = loadedRange.Intersect(visibleRange);
            var visiblePercent = CalculateVisiblePercent(loadedRange, visibleRange);
            var visibleCount = CountIntersecting(items, visibleRange);
            var totalCount = items?.Count ?? 0;
            var context = intersection.IsValid
                ? $"Loaded F{loadedRange.FirstFrame}-F{loadedRange.LastFrame}  |  " +
                  $"View {visiblePercent:0.#}%  |  Visible {visibleCount}/{totalCount}"
                : $"Loaded F{loadedRange.FirstFrame}-F{loadedRange.LastFrame}  |  " +
                  $"Current range is outside loaded data  |  Visible 0/{totalCount}";
            GUI.Label(
                new Rect(controlRect.x, plotRect.yMax + 2f, controlRect.width, 18f),
                context,
                EditorStyles.miniLabel);

            return BattleDebugTimelineInteraction.Handle(
                plotRect,
                loadedRange,
                showHelpTooltip: true,
                enableZoomAndPan: false);
        }

        internal static void ProjectDensity(
            IReadOnlyList<BattleDebugTimelineOverviewItem> items,
            BattleDiagnosticFrameRange loadedRange,
            int[] counts,
            int binCount,
            out int peak)
        {
            if (!loadedRange.IsValid)
                throw new ArgumentException("A valid loaded range is required.", nameof(loadedRange));
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            if (binCount <= 0 || binCount > counts.Length)
                throw new ArgumentOutOfRangeException(nameof(binCount));

            Array.Clear(counts, 0, binCount);
            peak = 1;
            if (items == null) return;

            for (var i = 0; i < items.Count; i++)
            {
                var itemRange = new BattleDiagnosticFrameRange(
                    items[i].StartFrame,
                    items[i].EndFrame);
                var clipped = itemRange.Intersect(loadedRange);
                if (!clipped.IsValid) continue;

                var firstBin = MapFrameToBin(clipped.FirstFrame, loadedRange, binCount);
                var lastBin = MapFrameToBin(clipped.LastFrame, loadedRange, binCount);
                for (var bin = firstBin; bin <= lastBin; bin++)
                {
                    counts[bin]++;
                    peak = Math.Max(peak, counts[bin]);
                }
            }
        }

        internal static float CalculateVisiblePercent(
            BattleDiagnosticFrameRange loadedRange,
            BattleDiagnosticFrameRange visibleRange)
        {
            var intersection = loadedRange.Intersect(visibleRange);
            if (!intersection.IsValid) return 0f;

            var loadedCount = (long)loadedRange.LastFrame - loadedRange.FirstFrame + 1L;
            var visibleCount = (long)intersection.LastFrame - intersection.FirstFrame + 1L;
            return visibleCount * 100f / loadedCount;
        }

        private static int CountIntersecting(
            IReadOnlyList<BattleDebugTimelineOverviewItem> items,
            BattleDiagnosticFrameRange visibleRange)
        {
            if (items == null || !visibleRange.IsValid) return 0;

            var count = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var itemRange = new BattleDiagnosticFrameRange(
                    items[i].StartFrame,
                    items[i].EndFrame);
                if (itemRange.Intersects(visibleRange)) count++;
            }
            return count;
        }

        private static int MapFrameToBin(
            int frame,
            BattleDiagnosticFrameRange range,
            int binCount)
        {
            var frameCount = (long)range.LastFrame - range.FirstFrame + 1L;
            var bin = (int)(((long)frame - range.FirstFrame) * binCount / frameCount);
            return Math.Min(bin, binCount - 1);
        }

        private static void DrawDensity(Rect rect, int[] counts, int binCount, int peak)
        {
            var binWidth = rect.width / binCount;
            for (var bin = 0; bin < binCount; bin++)
            {
                var height = counts[bin] / (float)Math.Max(1, peak) * (rect.height - 3f);
                if (height <= 0f) continue;
                EditorGUI.DrawRect(
                    new Rect(
                        rect.x + bin * binWidth,
                        rect.yMax - height,
                        Mathf.Max(1f, binWidth),
                        height),
                    new Color(0.36f, 0.62f, 0.82f, 0.72f));
            }
        }

        private static void DrawVisibleWindow(
            Rect rect,
            BattleDiagnosticFrameRange loadedRange,
            BattleDiagnosticFrameRange visibleRange)
        {
            var intersection = loadedRange.Intersect(visibleRange);
            if (!intersection.IsValid)
            {
                EditorGUI.DrawRect(rect, new Color(0.75f, 0.18f, 0.18f, 0.12f));
                return;
            }

            var frameCount = (long)loadedRange.LastFrame - loadedRange.FirstFrame + 1L;
            var startT = ((long)intersection.FirstFrame - loadedRange.FirstFrame) / (float)frameCount;
            var endT = ((long)intersection.LastFrame - loadedRange.FirstFrame + 1L) / (float)frameCount;
            var windowRect = new Rect(
                rect.x + rect.width * startT,
                rect.y,
                Mathf.Max(2f, rect.width * (endT - startT)),
                rect.height);
            windowRect.xMax = Mathf.Min(windowRect.xMax, rect.xMax);

            if (windowRect.x > rect.x)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.y, windowRect.x - rect.x, rect.height),
                    new Color(0f, 0f, 0f, 0.3f));
            }
            if (windowRect.xMax < rect.xMax)
            {
                EditorGUI.DrawRect(
                    new Rect(windowRect.xMax, rect.y, rect.xMax - windowRect.xMax, rect.height),
                    new Color(0f, 0f, 0f, 0.3f));
            }

            EditorGUI.DrawRect(new Rect(windowRect.x, windowRect.y, windowRect.width, 1f),
                new Color(0.35f, 0.75f, 1f, 0.95f));
            EditorGUI.DrawRect(new Rect(windowRect.x, windowRect.yMax - 1f, windowRect.width, 1f),
                new Color(0.35f, 0.75f, 1f, 0.95f));
            EditorGUI.DrawRect(new Rect(windowRect.x, windowRect.y, 1f, windowRect.height),
                new Color(0.35f, 0.75f, 1f, 0.95f));
            EditorGUI.DrawRect(new Rect(windowRect.xMax - 1f, windowRect.y, 1f, windowRect.height),
                new Color(0.35f, 0.75f, 1f, 0.95f));
        }

        private static void DrawCursor(
            Rect rect,
            BattleDiagnosticFrameRange range,
            int cursorFrame)
        {
            if (!range.Contains(cursorFrame)) return;
            var frameCount = (long)range.LastFrame - range.FirstFrame + 1L;
            var t = ((long)cursorFrame - range.FirstFrame + 0.5f) / frameCount;
            var x = rect.x + rect.width * t;
            EditorGUI.DrawRect(
                new Rect(Mathf.Round(x), rect.y, 1f, rect.height),
                new Color(1f, 0.82f, 0.22f, 0.95f));
        }
    }

    internal static class BattleDebugHistogram
    {
        public static BattleDebugTimelineInteractionResult Draw(
            IReadOnlyList<BattleDebugHistogramSample> samples,
            BattleDiagnosticFrameRange visibleRange,
            IReadOnlyList<BattleDebugHistogramSeries> series,
            int cursorFrame)
        {
            if (!visibleRange.IsValid || series == null || series.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No valid frame range is available for this chart.",
                    MessageType.Info);
                return default;
            }

            var chartRect = EditorGUILayout.GetControlRect(false, 104f);
            var plotRect = new Rect(
                chartRect.x + 2f,
                chartRect.y + 2f,
                Mathf.Max(1f, chartRect.width - 4f),
                78f);
            EditorGUI.DrawRect(plotRect, new Color(0f, 0f, 0f, 0.18f));

            var binCount = Mathf.Clamp(
                Mathf.FloorToInt(plotRect.width / 10f),
                8,
                BattleDebugHistogramSeries.MaximumBinCount);
            for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
            {
                Array.Clear(series[seriesIndex].Counts, 0, binCount);
            }

            var maxBinTotal = 1;
            if (samples != null)
            {
                for (var i = 0; i < samples.Count; i++)
                {
                    var sample = samples[i];
                    if (!visibleRange.Contains(sample.Frame) ||
                        sample.SeriesIndex < 0 ||
                        sample.SeriesIndex >= series.Count)
                    {
                        continue;
                    }

                    var bin = MapFrameToBin(sample.Frame, visibleRange, binCount);
                    series[sample.SeriesIndex].Counts[bin]++;
                    var total = 0;
                    for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
                    {
                        total += series[seriesIndex].Counts[bin];
                    }
                    maxBinTotal = Mathf.Max(maxBinTotal, total);
                }
            }

            var binWidth = plotRect.width / binCount;
            var frameCount = (long)visibleRange.LastFrame - visibleRange.FirstFrame + 1L;
            var currentEvent = Event.current;
            for (var bin = 0; bin < binCount; bin++)
            {
                var total = 0;
                for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
                {
                    total += series[seriesIndex].Counts[bin];
                }

                var x = plotRect.x + bin * binWidth;
                var fullHeight = total / (float)maxBinTotal * (plotRect.height - 3f);
                var yMax = plotRect.yMax;
                for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
                {
                    var count = series[seriesIndex].Counts[bin];
                    var height = total == 0 ? 0f : fullHeight * count / total;
                    if (height <= 0f) continue;

                    yMax -= height;
                    EditorGUI.DrawRect(
                        new Rect(
                            x + 1f,
                            yMax,
                            Mathf.Max(1f, binWidth - 2f),
                            height),
                        series[seriesIndex].Color);
                }

                var hitRect = new Rect(x, plotRect.y, binWidth, plotRect.height);
                var binStart = visibleRange.FirstFrame +
                               (int)(bin * frameCount / binCount);
                var binEnd = visibleRange.FirstFrame +
                             (int)((bin + 1L) * frameCount / binCount) - 1;
                binEnd = Mathf.Clamp(binEnd, binStart, visibleRange.LastFrame);
                if (hitRect.Contains(currentEvent.mousePosition))
                {
                    GUI.Label(
                        hitRect,
                        new GUIContent(string.Empty, BuildTooltip(
                            binStart,
                            binEnd,
                            bin,
                            total,
                            series)));
                }
            }

            DrawCursor(plotRect, visibleRange, cursorFrame);
            var interaction = BattleDebugTimelineInteraction.Handle(
                plotRect,
                visibleRange,
                showHelpTooltip: false);
            var labelRect = new Rect(chartRect.x, plotRect.yMax + 3f, chartRect.width, 18f);
            GUI.Label(
                labelRect,
                $"F{visibleRange.FirstFrame}  ->  F{visibleRange.LastFrame}    Peak {maxBinTotal}/bin",
                EditorStyles.miniLabel);
            return interaction;
        }

        internal static int MapFrameToBin(
            int frame,
            BattleDiagnosticFrameRange range,
            int binCount)
        {
            if (!range.IsValid) throw new ArgumentException("A valid range is required.", nameof(range));
            if (!range.Contains(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (binCount <= 0) throw new ArgumentOutOfRangeException(nameof(binCount));

            var frameCount = (long)range.LastFrame - range.FirstFrame + 1L;
            var bin = (int)(((long)frame - range.FirstFrame) * binCount / frameCount);
            return Math.Min(bin, binCount - 1);
        }

        private static string BuildTooltip(
            int binStart,
            int binEnd,
            int bin,
            int total,
            IReadOnlyList<BattleDebugHistogramSeries> series)
        {
            var tooltip = $"F{binStart}-F{binEnd}: {total}";
            for (var i = 0; i < series.Count; i++)
            {
                tooltip += $"\n{series[i].Label}={series[i].Counts[bin]}";
            }
            return tooltip;
        }

        private static void DrawCursor(
            Rect plotRect,
            BattleDiagnosticFrameRange range,
            int cursorFrame)
        {
            if (!range.Contains(cursorFrame)) return;

            var frameCount = (long)range.LastFrame - range.FirstFrame + 1L;
            var t = ((long)cursorFrame - range.FirstFrame + 0.5f) / frameCount;
            var x = plotRect.x + plotRect.width * t;
            EditorGUI.DrawRect(
                new Rect(Mathf.Round(x), plotRect.y, 1f, plotRect.height),
                new Color(1f, 0.82f, 0.22f, 0.95f));
        }
    }

    internal readonly struct BattleDebugWaterfallItem
    {
        public BattleDebugWaterfallItem(
            long id,
            string label,
            string tooltip,
            int startFrame,
            int endFrame,
            int depth,
            bool selected,
            Color color)
        {
            Id = id;
            Label = label ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            StartFrame = startFrame;
            EndFrame = endFrame;
            Depth = Math.Max(0, depth);
            Selected = selected;
            Color = color;
        }

        public long Id { get; }
        public string Label { get; }
        public string Tooltip { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public int Depth { get; }
        public bool Selected { get; }
        public Color Color { get; }
    }

    internal static class BattleDebugWaterfall
    {
        public const int DefaultRowLimit = 300;
        private const float RowHeight = 21f;

        public static BattleDebugWaterfallDrawResult Draw(
            IReadOnlyList<BattleDebugWaterfallItem> items,
            BattleDiagnosticFrameRange visibleRange,
            int cursorFrame,
            ref Vector2 scroll,
            float labelWidth = 150f,
            int rowLimit = DefaultRowLimit)
        {
            if (!visibleRange.IsValid || items == null || items.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No trace spans intersect the current frame range.",
                    MessageType.Info);
                return default;
            }

            var rulerRect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.DrawRect(rulerRect, new Color(0f, 0f, 0f, 0.12f));
            GUI.Label(
                new Rect(rulerRect.x, rulerRect.y, labelWidth - 2f, rulerRect.height),
                "Node",
                EditorStyles.miniBoldLabel);
            var rulerTimelineRect = new Rect(
                rulerRect.x + labelWidth,
                rulerRect.y,
                Mathf.Max(1f, rulerRect.width - labelWidth),
                rulerRect.height);
            GUI.Label(
                rulerTimelineRect,
                $"F{visibleRange.FirstFrame}",
                EditorStyles.miniLabel);
            var lastFrameContent = new GUIContent($"F{visibleRange.LastFrame}");
            var lastFrameWidth = EditorStyles.miniLabel.CalcSize(lastFrameContent).x;
            GUI.Label(
                new Rect(
                    rulerTimelineRect.xMax - lastFrameWidth,
                    rulerTimelineRect.y,
                    lastFrameWidth,
                    rulerTimelineRect.height),
                lastFrameContent,
                EditorStyles.miniLabel);
            var timelineInteraction = BattleDebugTimelineInteraction.Handle(
                rulerTimelineRect,
                visibleRange);

            var clickedId = 0L;
            var visibleItemCount = CountVisibleItems(items, visibleRange);
            var drawnCount = 0;
            scroll = EditorGUILayout.BeginScrollView(
                scroll,
                GUILayout.MinHeight(180f),
                GUILayout.MaxHeight(380f));
            for (var i = 0; i < items.Count && drawnCount < rowLimit; i++)
            {
                var item = items[i];
                if (!TryClipSpan(
                        item.StartFrame,
                        item.EndFrame,
                        visibleRange,
                        out var clippedRange))
                {
                    continue;
                }

                drawnCount++;
                var rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
                if (item.Selected)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.45f, 0.72f, 0.22f));
                }

                var labelRect = new Rect(rowRect.x, rowRect.y, labelWidth - 2f, rowRect.height);
                var labelIndent = Mathf.Min(40f, item.Depth * 6f);
                labelRect.x += labelIndent;
                labelRect.width -= labelIndent;
                GUI.Label(
                    labelRect,
                    new GUIContent(item.Label, item.Tooltip),
                    EditorStyles.miniLabel);

                var timelineRect = new Rect(
                    rowRect.x + labelWidth,
                    rowRect.y + 2f,
                    Mathf.Max(1f, rowRect.width - labelWidth - 2f),
                    rowRect.height - 4f);
                EditorGUI.DrawRect(timelineRect, new Color(0f, 0f, 0f, 0.12f));
                var frameCount = (long)visibleRange.LastFrame - visibleRange.FirstFrame + 1L;
                var startT = ((long)clippedRange.FirstFrame - visibleRange.FirstFrame) / (float)frameCount;
                var endT = ((long)clippedRange.LastFrame - visibleRange.FirstFrame + 1L) / (float)frameCount;
                var barRect = new Rect(
                    timelineRect.x + timelineRect.width * startT,
                    timelineRect.y + 2f,
                    Mathf.Max(3f, timelineRect.width * Mathf.Max(0f, endT - startT)),
                    timelineRect.height - 4f);
                barRect.xMax = Mathf.Min(barRect.xMax, timelineRect.xMax);
                EditorGUI.DrawRect(barRect, item.Color);
                GUI.Label(
                    barRect,
                    new GUIContent(
                        string.Empty,
                        $"F{item.StartFrame} -> F{item.EndFrame}"));
                DrawCursor(timelineRect, visibleRange, cursorFrame);

                var currentEvent = Event.current;
                if (currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 &&
                    rowRect.Contains(currentEvent.mousePosition))
                {
                    clickedId = item.Id;
                    currentEvent.Use();
                }
            }
            EditorGUILayout.EndScrollView();

            if (visibleItemCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No trace spans intersect the current shared frame range. " +
                    "Use the overview to select a loaded range.",
                    MessageType.Info);
            }
            else if (visibleItemCount > rowLimit)
            {
                EditorGUILayout.HelpBox(
                    $"Waterfall is limited to the first {rowLimit} of {visibleItemCount} visible nodes.",
                    MessageType.Info);
            }
            return new BattleDebugWaterfallDrawResult(clickedId, timelineInteraction);
        }

        internal static bool TryClipSpan(
            int startFrame,
            int endFrame,
            BattleDiagnosticFrameRange visibleRange,
            out BattleDiagnosticFrameRange clippedRange)
        {
            var itemRange = new BattleDiagnosticFrameRange(
                Math.Min(startFrame, endFrame),
                Math.Max(startFrame, endFrame));
            clippedRange = itemRange.Intersect(visibleRange);
            return clippedRange.IsValid;
        }

        private static int CountVisibleItems(
            IReadOnlyList<BattleDebugWaterfallItem> items,
            BattleDiagnosticFrameRange visibleRange)
        {
            var count = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (TryClipSpan(
                        items[i].StartFrame,
                        items[i].EndFrame,
                        visibleRange,
                        out _))
                {
                    count++;
                }
            }
            return count;
        }

        private static void DrawCursor(
            Rect timelineRect,
            BattleDiagnosticFrameRange range,
            int cursorFrame)
        {
            if (!range.Contains(cursorFrame)) return;

            var frameCount = (long)range.LastFrame - range.FirstFrame + 1L;
            var t = ((long)cursorFrame - range.FirstFrame + 0.5f) / frameCount;
            var x = timelineRect.x + timelineRect.width * t;
            EditorGUI.DrawRect(
                new Rect(Mathf.Round(x), timelineRect.y, 1f, timelineRect.height),
                new Color(1f, 0.82f, 0.22f, 0.95f));
        }
    }

    internal readonly struct BattleDebugWaterfallDrawResult
    {
        public BattleDebugWaterfallDrawResult(
            long selectedId,
            BattleDebugTimelineInteractionResult timelineInteraction)
        {
            SelectedId = selectedId;
            TimelineInteraction = timelineInteraction;
        }

        public long SelectedId { get; }
        public BattleDebugTimelineInteractionResult TimelineInteraction { get; }
    }

    internal static class BattleDebugTimeRangeSelector
    {
        private const int DefaultFocusRadius = 60;
        private static readonly GUIContent[] ModeLabels =
        {
            new GUIContent("Auto", "Use the complete range exposed by each visualization."),
            new GUIContent("Fixed", "Use one shared frame range across diagnostics widgets.")
        };

        public static void Draw(
            BattleDiagnosticWorkspaceState workspaceState,
            float availableWidth,
            Action requestRepaint)
        {
            if (workspaceState == null) return;

            if (availableWidth < 700f)
            {
                DrawCompact(workspaceState, requestRepaint);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                "Shared Range",
                EditorStyles.miniBoldLabel,
                GUILayout.Width(78f));
            DrawHistoryButtons(workspaceState, requestRepaint);
            DrawMode(workspaceState, 108f, requestRepaint);
            DrawRangeFields(workspaceState, 62f, requestRepaint);
            DrawRangeNavigation(workspaceState, requestRepaint);
            GUILayout.FlexibleSpace();
            DrawFocusAndReset(workspaceState, false, requestRepaint);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawCompact(
            BattleDiagnosticWorkspaceState workspaceState,
            Action requestRepaint)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Range", EditorStyles.miniBoldLabel, GUILayout.Width(36f));
            DrawHistoryButtons(workspaceState, requestRepaint, 20f);
            DrawMode(workspaceState, 76f, requestRepaint);
            GUILayout.FlexibleSpace();
            DrawFocusAndReset(workspaceState, true, requestRepaint);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(4f);
            DrawRangeFields(workspaceState, 48f, requestRepaint);
            GUILayout.FlexibleSpace();
            DrawRangeNavigation(workspaceState, requestRepaint);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawHistoryButtons(
            BattleDiagnosticWorkspaceState workspaceState,
            Action requestRepaint,
            float buttonWidth = 22f)
        {
            EditorGUI.BeginDisabledGroup(!workspaceState.CanGoBackTimeRange);
            if (GUILayout.Button(
                    new GUIContent("↶", "Return to the previous shared frame range."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(buttonWidth)) &&
                workspaceState.GoBackTimeRange())
            {
                requestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!workspaceState.CanGoForwardTimeRange);
            if (GUILayout.Button(
                    new GUIContent("↷", "Move forward to the next shared frame range."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(buttonWidth)) &&
                workspaceState.GoForwardTimeRange())
            {
                requestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawMode(
            BattleDiagnosticWorkspaceState workspaceState,
            float width,
            Action requestRepaint)
        {
            var timeRange = workspaceState.TimeRange;
            var mode = GUILayout.Toolbar(
                timeRange.IsFixed ? 1 : 0,
                ModeLabels,
                EditorStyles.toolbarButton,
                GUILayout.Width(width));
            if (mode == 0 && timeRange.IsFixed)
            {
                workspaceState.ClearTimeRange();
                requestRepaint?.Invoke();
                timeRange = workspaceState.TimeRange;
            }
            else if (mode == 1 && timeRange.IsAuto)
            {
                var cursor = workspaceState.FrameCursor.HasFrame
                    ? workspaceState.FrameCursor.Frame
                    : 0;
                workspaceState.FocusTimeRange(cursor, DefaultFocusRadius);
                requestRepaint?.Invoke();
            }
        }

        private static void DrawRangeFields(
            BattleDiagnosticWorkspaceState workspaceState,
            float fieldWidth,
            Action requestRepaint)
        {
            var timeRange = workspaceState.TimeRange;
            var firstFrame = timeRange.IsFixed
                ? timeRange.FirstFrame
                : Math.Max(0, workspaceState.FrameCursor.Frame - DefaultFocusRadius);
            var lastFrame = timeRange.IsFixed
                ? timeRange.LastFrame
                : Math.Max(firstFrame, workspaceState.FrameCursor.Frame);
            GUILayout.Label("F", EditorStyles.miniLabel, GUILayout.Width(10f));
            var nextFirstFrame = EditorGUILayout.DelayedIntField(
                firstFrame,
                EditorStyles.toolbarTextField,
                GUILayout.Width(fieldWidth));
            GUILayout.Label("-", EditorStyles.miniLabel, GUILayout.Width(8f));
            var nextLastFrame = EditorGUILayout.DelayedIntField(
                lastFrame,
                EditorStyles.toolbarTextField,
                GUILayout.Width(fieldWidth));
            if (nextFirstFrame != firstFrame || nextLastFrame != lastFrame)
            {
                workspaceState.SetTimeRange(
                    Math.Max(0, nextFirstFrame),
                    Math.Max(0, nextLastFrame));
                requestRepaint?.Invoke();
            }
        }

        private static void DrawRangeNavigation(
            BattleDiagnosticWorkspaceState workspaceState,
            Action requestRepaint)
        {
            var timeRange = workspaceState.TimeRange;
            EditorGUI.BeginDisabledGroup(!timeRange.IsFixed);
            var frameCount = timeRange.IsFixed
                ? (long)timeRange.LastFrame - timeRange.FirstFrame + 1L
                : 0L;
            var panFrames = (int)Math.Max(1L, Math.Min(int.MaxValue, frameCount / 4L));
            if (GUILayout.Button(
                    new GUIContent("←", "Pan left by one quarter of the current range."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(24f)) &&
                workspaceState.PanTimeRange(-panFrames))
            {
                requestRepaint?.Invoke();
            }
            if (GUILayout.Button(
                    new GUIContent("→", "Pan right by one quarter of the current range."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(24f)) &&
                workspaceState.PanTimeRange(panFrames))
            {
                requestRepaint?.Invoke();
            }

            var anchorFrame = ResolveZoomAnchor(workspaceState);
            if (GUILayout.Button(
                    new GUIContent("-", "Zoom out around the frame cursor."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(24f)) &&
                workspaceState.ZoomTimeRange(anchorFrame, 2d))
            {
                requestRepaint?.Invoke();
            }
            if (GUILayout.Button(
                    new GUIContent("+", "Zoom in around the frame cursor."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(24f)) &&
                workspaceState.ZoomTimeRange(anchorFrame, 0.5d))
            {
                requestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawFocusAndReset(
            BattleDiagnosticWorkspaceState workspaceState,
            bool compact,
            Action requestRepaint)
        {
            EditorGUI.BeginDisabledGroup(!workspaceState.FrameCursor.HasFrame);
            if (GUILayout.Button(
                    new GUIContent(compact ? "Focus" : "Focus Cursor", "Center a 120-frame range on the current frame."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compact ? 40f : 82f)))
            {
                workspaceState.FocusTimeRange(
                    workspaceState.FrameCursor.Frame,
                    DefaultFocusRadius);
                requestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(workspaceState.TimeRange.IsAuto);
            if (GUILayout.Button(
                    new GUIContent("Reset", "Return all diagnostics widgets to their automatic data range."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compact ? 40f : 44f)))
            {
                workspaceState.ClearTimeRange();
                requestRepaint?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
        }

        private static int ResolveZoomAnchor(BattleDiagnosticWorkspaceState workspaceState)
        {
            var range = workspaceState.TimeRange.Range;
            if (range.Contains(workspaceState.FrameCursor.Frame))
            {
                return workspaceState.FrameCursor.Frame;
            }

            return range.FirstFrame + (range.LastFrame - range.FirstFrame) / 2;
        }
    }

    internal static class BattleDebugFrameMetricHistory
    {
        private const int MaximumSeries = 6;
        private static readonly BattleDebugTimelineOverviewBuffer OverviewBuffer =
            new BattleDebugTimelineOverviewBuffer();
        private static readonly Dictionary<BattleDiagnosticMetricCategory, MetricHistoryCacheEntry> Cache =
            new Dictionary<BattleDiagnosticMetricCategory, MetricHistoryCacheEntry>();
        private static long _nextRequestId;
        private static bool _showProfileDifferences;
        private static long _comparedRegistryRevision = -1L;
        private static BattleDiagnosticResolvedMetricProfile _comparedCapturedProfile;
        private static BattleDiagnosticMetricProfileComparison _profileComparison;

        public static bool IsAvailable(
            in BattleDebugContext ctx,
            BattleDiagnosticMetricCategory category)
        {
            return BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session) &&
                   session.SessionInfo.Supports(BattleDiagnosticCapabilities.FrameMetrics) &&
                   session is IBattleDiagnosticMetricSession;
        }

        public static bool Draw(
            in BattleDebugContext ctx,
            BattleDiagnosticMetricCategory category,
            string title)
        {
            if (!BattleDebugDiagnosticSessionResolver.TryResolve(in ctx, out var session) ||
                !session.SessionInfo.Supports(BattleDiagnosticCapabilities.FrameMetrics) ||
                !(session is IBattleDiagnosticMetricSession metricSession))
            {
                return false;
            }

            var visibleRange = ResolveRange(in ctx);
            if (!visibleRange.IsValid)
            {
                EditorGUILayout.HelpBox("No shared frame range is available for metric history.", MessageType.Info);
                return true;
            }

            var cache = GetOrRefresh(session, metricSession, category, visibleRange);
            if (!cache.Status.CanDisplayResults)
            {
                EditorGUILayout.HelpBox(cache.Status.Message, MessageType.Info);
                return true;
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawProfileSummary(in ctx, session, cache.Profile);
            if (cache.AggregateCount == 0)
            {
                EditorGUILayout.HelpBox("No metric samples intersect the shared frame range.", MessageType.Info);
                return true;
            }

            DrawAssessmentSummary(cache.CompoundAssessments, cache.Assessments);

            ApplyInteraction(
                in ctx,
                BattleDebugTimelineOverview.Draw(
                    cache.OverviewItems,
                    visibleRange,
                    visibleRange,
                    ctx.WorkspaceState?.FrameCursor.Frame ?? BattleDiagnosticFrames.Invalid,
                    OverviewBuffer));

            var seriesCount = Math.Min(MaximumSeries, cache.Series.Count);
            for (var i = 0; i < seriesCount; i++)
            {
                DrawSeries(in ctx, cache.Series[i], visibleRange, i);
            }
            if (cache.Series.Count > MaximumSeries)
                EditorGUILayout.LabelField($"Showing {MaximumSeries} of {cache.Series.Count} metric series.", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"{cache.SampleCount} samples  |  {cache.AggregateCount} visible buckets",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);
            return true;
        }

        private static void DrawProfileSummary(
            in BattleDebugContext ctx,
            IBattleDiagnosticReadOnlySession session,
            BattleDiagnosticResolvedMetricProfile effectiveProfile)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Threshold Profile", effectiveProfile.Name);
            var settings = EditorGUIUtility.IconContent(
                "d_SettingsIcon",
                "Open the active BattleDebug metric profile asset");
            if (GUILayout.Button(settings, EditorStyles.iconButton, GUILayout.Width(22f), GUILayout.Height(18f)))
                BattleDiagnosticMetricProfileAssetSync.OpenOrCreateAsset();
            EditorGUILayout.EndHorizontal();

            var capturedProfile = (session as IBattleDiagnosticMetricProfileSession)?.MetricProfile;
            if (capturedProfile == null)
            {
                if (ctx.IsOffline)
                    EditorGUILayout.HelpBox(
                        "This artifact predates captured metric profiles. Findings use the current project profile.",
                        MessageType.Info);
                return;
            }

            var registryRevision = BattleDiagnosticMetricProfileRegistry.Revision;
            if (!ReferenceEquals(_comparedCapturedProfile, capturedProfile) ||
                _comparedRegistryRevision != registryRevision)
            {
                var currentProfile = BattleDiagnosticMetricProfileRegistry.Resolve();
                _profileComparison = BattleDiagnosticMetricProfileComparer.Compare(
                    capturedProfile,
                    currentProfile);
                _comparedCapturedProfile = capturedProfile;
                _comparedRegistryRevision = registryRevision;
            }

            if (_profileComparison == null) return;
            if (!_profileComparison.HasDifferences)
            {
                EditorGUILayout.LabelField(
                    "Current Project Profile",
                    _profileComparison.Current.Name + "  (matches capture)",
                    EditorStyles.miniLabel);
                return;
            }

            var contextDifference = _profileComparison.ContextMatches ? 0 : 1;
            EditorGUILayout.HelpBox(
                "Capture uses '" + capturedProfile.Name + "'; current project resolves '" +
                _profileComparison.Current.Name + "'. " +
                (_profileComparison.ThresholdDifferences.Count + contextDifference) +
                " profile difference(s) detected. Historical findings still use the captured profile.",
                MessageType.Warning);
            _showProfileDifferences = EditorGUILayout.Foldout(
                _showProfileDifferences,
                "Profile Differences",
                true);
            if (!_showProfileDifferences) return;

            EditorGUI.indentLevel++;
            if (!_profileComparison.ContextMatches)
            {
                EditorGUILayout.LabelField(
                    "Context",
                    FormatContext(capturedProfile.Context) + "  ->  " +
                    FormatContext(_profileComparison.Current.Context),
                    EditorStyles.miniLabel);
            }
            for (var i = 0; i < _profileComparison.ThresholdDifferences.Count; i++)
            {
                var difference = _profileComparison.ThresholdDifferences[i];
                EditorGUILayout.LabelField(
                    difference.DisplayName,
                    FormatDifference(in difference),
                    EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        private static string FormatContext(in BattleDiagnosticMetricProfileContext context)
        {
            return ValueOrWildcard(context.Project) + " / " +
                   ValueOrWildcard(context.GameMode) + " / " +
                   ValueOrWildcard(context.NetworkMode) + " / " +
                   ValueOrWildcard(context.DeviceTier);
        }

        private static string ValueOrWildcard(string value) =>
            string.IsNullOrEmpty(value) ? "*" : value;

        private static string FormatDifference(in BattleDiagnosticMetricProfileDifference difference)
        {
            var builder = new StringBuilder();
            if (difference.WarningChanged)
                AppendDifference(
                    builder,
                    "W",
                    difference.CapturedWarningThreshold,
                    difference.CurrentWarningThreshold,
                    difference.Unit);
            if (difference.CriticalChanged)
                AppendDifference(
                    builder,
                    "C",
                    difference.CapturedCriticalThreshold,
                    difference.CurrentCriticalThreshold,
                    difference.Unit);
            if (difference.SuggestedRangeChanged)
            {
                if (builder.Length > 0) builder.Append("  |  ");
                builder.Append("Range ")
                    .Append(FormatRange(
                        difference.CapturedSuggestedMinimum,
                        difference.CapturedSuggestedMaximum,
                        difference.Unit))
                    .Append(" -> ")
                    .Append(FormatRange(
                        difference.CurrentSuggestedMinimum,
                        difference.CurrentSuggestedMaximum,
                        difference.Unit));
            }
            return builder.ToString();
        }

        private static void AppendDifference(
            StringBuilder builder,
            string label,
            double captured,
            double current,
            string unit)
        {
            if (builder.Length > 0) builder.Append("  |  ");
            builder.Append(label).Append(' ')
                .Append(FormatValue(captured, unit))
                .Append(" -> ")
                .Append(FormatValue(current, unit));
        }

        private static string FormatRange(double minimum, double maximum, string unit)
        {
            if (double.IsNaN(minimum) || double.IsNaN(maximum)) return "none";
            return FormatValue(minimum, unit) + "-" + FormatValue(maximum, unit);
        }

        private static MetricHistoryCacheEntry GetOrRefresh(
            IBattleDiagnosticReadOnlySession session,
            IBattleDiagnosticMetricSession metricSession,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticFrameRange range)
        {
            var revision = metricSession.MetricStoreRevision;
            var embeddedProfile = (session as IBattleDiagnosticMetricProfileSession)?.MetricProfile;
            var profileRevision = embeddedProfile == null
                ? BattleDiagnosticMetricProfileRegistry.Revision
                : -1L;
            if (Cache.TryGetValue(category, out var cached) &&
                cached.Matches(session, revision, profileRevision, embeddedProfile, in range))
            {
                return cached;
            }

            var profile = embeddedProfile ?? BattleDiagnosticMetricProfileRegistry.Resolve();

            var aggregates = new List<BattleDiagnosticMetricAggregate>();
            BattleDiagnosticQueryStatus status = default;
            var storeRevision = 0L;
            var offset = 0;
            do
            {
                var result = metricSession.QueryMetricAggregates(new BattleDiagnosticMetricAggregateQuery(
                    NextRequestId(),
                    range,
                    new BattleDiagnosticPageRequest(
                        storeRevision,
                        offset,
                        BattleDiagnosticPageRequest.MaximumPageSize),
                    category));
                status = result.Status;
                if (!status.CanDisplayResults)
                {
                    aggregates.Clear();
                    break;
                }

                for (var i = 0; i < result.Items.Count; i++) aggregates.Add(result.Items[i]);
                if (!status.HasMore || result.Items.Count == 0) break;
                storeRevision = status.StoreRevision;
                offset += result.Items.Count;
            } while (true);

            var refreshed = new MetricHistoryCacheEntry(
                session,
                status.StoreRevision,
                profileRevision,
                profile,
                in range,
                in status,
                aggregates);
            Cache[category] = refreshed;
            return refreshed;
        }

        private static void DrawSeries(
            in BattleDebugContext ctx,
            MetricSeriesView series,
            BattleDiagnosticFrameRange range,
            int colorIndex)
        {
            if (series.Aggregates.Count == 0) return;

            var rect = EditorGUILayout.GetControlRect(false, 42f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.12f));
            if (series.Severity != BattleDiagnosticMetricSeverity.Normal)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.y, 3f, rect.height),
                    SeverityColor(series.Severity));
            }
            GUI.Label(
                new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f),
                $"{series.Label}  {FormatValue(series.LatestValue, series.Unit)}  " +
                $"[{FormatValue(series.MinimumValue, series.Unit)}, {FormatValue(series.MaximumValue, series.Unit)}]",
                EditorStyles.miniLabel);
            var plot = new Rect(rect.x + 2f, rect.y + 18f, rect.width - 4f, 22f);
            if (Event.current.type == EventType.Repaint)
            {
                var frameSpan = Math.Max(1L, (long)range.LastFrame - range.FirstFrame);
                var valueSpan = Math.Max(0.000001d, series.ScaleMaximum - series.ScaleMinimum);
                for (var i = 0; i < series.Aggregates.Count; i++)
                {
                    var aggregate = series.Aggregates[i];
                    var x = plot.x + plot.width * (((long)aggregate.LastFrame - range.FirstFrame) / (float)frameSpan);
                    var y = plot.yMax - plot.height *
                        (float)((aggregate.LastValue - series.ScaleMinimum) / valueSpan);
                    series.Points[i] = new Vector3(x, y, 0f);
                }
                Handles.BeginGUI();
                var color = series.Severity == BattleDiagnosticMetricSeverity.Normal
                    ? SeriesColor(colorIndex)
                    : SeverityColor(series.Severity);
                Handles.color = new Color(color.r, color.g, color.b, 0.35f);
                for (var i = 0; i < series.Aggregates.Count; i++)
                {
                    var aggregate = series.Aggregates[i];
                    if (aggregate.MinimumValue.Equals(aggregate.MaximumValue)) continue;
                    var x = series.Points[i].x;
                    var minY = plot.yMax - plot.height *
                        (float)((aggregate.MinimumValue - series.ScaleMinimum) / valueSpan);
                    var maxY = plot.yMax - plot.height *
                        (float)((aggregate.MaximumValue - series.ScaleMinimum) / valueSpan);
                    Handles.DrawLine(new Vector3(x, minY, 0f), new Vector3(x, maxY, 0f));
                }
                Handles.color = color;
                if (series.Points.Length == 1) Handles.DrawSolidDisc(series.Points[0], Vector3.forward, 2f);
                else Handles.DrawAAPolyLine(2f, series.Points);
                Handles.EndGUI();
            }

            ApplyInteraction(in ctx, BattleDebugTimelineInteraction.Handle(plot, range));
        }

        private static void DrawAssessmentSummary(
            IReadOnlyList<BattleDiagnosticCompoundMetricAssessment> compounds,
            IReadOnlyList<BattleDiagnosticMetricAssessment> assessments)
        {
            var compoundCount = compounds?.Count ?? 0;
            var assessmentCount = assessments?.Count ?? 0;
            if (compoundCount == 0 && assessmentCount == 0) return;
            var builder = new StringBuilder();
            var severity = BattleDiagnosticMetricSeverity.Warning;
            var displayed = 0;
            var covered = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < compoundCount; i++)
            {
                var compound = compounds[i];
                covered.Add(compound.Primary.Descriptor.Metric + "\n" + compound.Dimension);
                covered.Add(compound.Secondary.Descriptor.Metric + "\n" + compound.Dimension);
            }
            var totalFindings = compoundCount;
            for (var i = 0; i < assessmentCount; i++)
            {
                var assessment = assessments[i];
                if (!covered.Contains(assessment.Descriptor.Metric + "\n" + assessment.Dimension))
                    totalFindings++;
            }
            for (var i = 0; i < compoundCount && displayed < 3; i++)
            {
                var compound = compounds[i];
                if (displayed++ > 0) builder.AppendLine();
                if (compound.Severity > severity) severity = compound.Severity;
                builder.Append(compound.Rule.DisplayName);
                if (!string.IsNullOrEmpty(compound.Dimension))
                    builder.Append(" [").Append(compound.Dimension).Append(']');
                builder.Append(" (F").Append(compound.FirstFrame)
                    .Append("-F").Append(compound.LastFrame).Append(')');
            }
            for (var i = 0; i < assessmentCount && displayed < 3; i++)
            {
                var assessment = assessments[i];
                if (covered.Contains(assessment.Descriptor.Metric + "\n" + assessment.Dimension)) continue;
                if (displayed++ > 0) builder.AppendLine();
                if (assessment.Severity > severity) severity = assessment.Severity;
                builder.Append(assessment.Descriptor.DisplayName);
                if (!string.IsNullOrEmpty(assessment.Dimension))
                    builder.Append(" [").Append(assessment.Dimension).Append(']');
                builder.Append(assessment.Descriptor.AssessmentMode == BattleDiagnosticMetricAssessmentMode.WindowDeltaHigh
                    ? " increased by "
                    : assessment.Descriptor.AssessmentMode == BattleDiagnosticMetricAssessmentMode.LatestHigh
                        ? " is "
                        : " peaked at ");
                builder.Append(FormatValue(assessment.ActualValue, assessment.Descriptor.Unit));
                builder.Append(" (threshold ")
                    .Append(FormatValue(assessment.ActiveThreshold, assessment.Descriptor.Unit))
                    .Append(", F").Append(assessment.FirstFrame)
                    .Append("-F").Append(assessment.LastFrame).Append(')');
            }
            var remaining = totalFindings - displayed;
            if (remaining > 0)
                builder.AppendLine().Append('+').Append(remaining).Append(" additional findings");
            EditorGUILayout.HelpBox(
                builder.ToString(),
                severity == BattleDiagnosticMetricSeverity.Critical ? MessageType.Error : MessageType.Warning);
        }

        private static string FormatValue(double value, string unit)
        {
            if (string.Equals(unit, "flag", StringComparison.Ordinal)) return value >= 0.5d ? "active" : "inactive";
            return string.IsNullOrEmpty(unit) ? $"{value:0.###}" : $"{value:0.###} {unit}";
        }

        private static long NextRequestId()
        {
            if (_nextRequestId == long.MaxValue) _nextRequestId = 0L;
            return ++_nextRequestId;
        }

        private sealed class MetricHistoryCacheEntry
        {
            public MetricHistoryCacheEntry(
                IBattleDiagnosticReadOnlySession session,
                long revision,
                long profileRevision,
                BattleDiagnosticResolvedMetricProfile profile,
                in BattleDiagnosticFrameRange range,
                in BattleDiagnosticQueryStatus status,
                IReadOnlyList<BattleDiagnosticMetricAggregate> aggregates)
            {
                Session = session;
                Revision = revision;
                ProfileRevision = profileRevision;
                EmbeddedProfile = (session as IBattleDiagnosticMetricProfileSession)?.MetricProfile;
                Profile = profile;
                Range = range;
                Status = status;
                AggregateCount = aggregates?.Count ?? 0;
                OverviewItems = new List<BattleDebugTimelineOverviewItem>(AggregateCount);
                Assessments = BattleDiagnosticFrameMetricCatalog.Evaluate(aggregates, profile);
                CompoundAssessments = BattleDiagnosticFrameMetricCatalog.EvaluateCompounds(Assessments);
                Series = BuildSeries(aggregates, Assessments, profile, OverviewItems, out var sampleCount);
                SampleCount = sampleCount;
            }

            private IBattleDiagnosticReadOnlySession Session { get; }
            private long Revision { get; }
            private long ProfileRevision { get; }
            private BattleDiagnosticResolvedMetricProfile EmbeddedProfile { get; }
            private BattleDiagnosticFrameRange Range { get; }
            public BattleDiagnosticQueryStatus Status { get; }
            public BattleDiagnosticResolvedMetricProfile Profile { get; }
            public int AggregateCount { get; }
            public int SampleCount { get; }
            public List<BattleDebugTimelineOverviewItem> OverviewItems { get; }
            public IReadOnlyList<BattleDiagnosticMetricAssessment> Assessments { get; }
            public IReadOnlyList<BattleDiagnosticCompoundMetricAssessment> CompoundAssessments { get; }
            public List<MetricSeriesView> Series { get; }

            public bool Matches(
                IBattleDiagnosticReadOnlySession session,
                long revision,
                long profileRevision,
                BattleDiagnosticResolvedMetricProfile embeddedProfile,
                in BattleDiagnosticFrameRange range)
            {
                return ReferenceEquals(Session, session) && Revision == revision && ProfileRevision == profileRevision &&
                       ReferenceEquals(EmbeddedProfile, embeddedProfile) &&
                       Range.FirstFrame == range.FirstFrame && Range.LastFrame == range.LastFrame;
            }

            private static List<MetricSeriesView> BuildSeries(
                IReadOnlyList<BattleDiagnosticMetricAggregate> aggregates,
                IReadOnlyList<BattleDiagnosticMetricAssessment> assessments,
                BattleDiagnosticResolvedMetricProfile profile,
                List<BattleDebugTimelineOverviewItem> overviewItems,
                out int sampleCount)
            {
                var map = new Dictionary<string, List<BattleDiagnosticMetricAggregate>>(StringComparer.Ordinal);
                sampleCount = 0;
                if (aggregates != null)
                {
                    for (var i = 0; i < aggregates.Count; i++)
                    {
                        var aggregate = aggregates[i];
                        sampleCount += aggregate.SampleCount;
                        overviewItems.Add(new BattleDebugTimelineOverviewItem(
                            aggregate.FirstFrame,
                            aggregate.LastFrame));
                        var key = string.IsNullOrEmpty(aggregate.Dimension)
                            ? aggregate.Metric
                            : aggregate.Metric + " [" + aggregate.Dimension + "]";
                        if (!map.TryGetValue(key, out var values))
                        {
                            values = new List<BattleDiagnosticMetricAggregate>();
                            map.Add(key, values);
                        }
                        values.Add(aggregate);
                    }
                }

                var result = new List<MetricSeriesView>(map.Count);
                foreach (var pair in map)
                {
                    var severity = BattleDiagnosticMetricSeverity.Normal;
                    var first = pair.Value[0];
                    for (var i = 0; i < assessments.Count; i++)
                    {
                        var assessment = assessments[i];
                        if (!string.Equals(assessment.Descriptor.Metric, first.Metric, StringComparison.Ordinal) ||
                            !string.Equals(assessment.Dimension, first.Dimension, StringComparison.Ordinal)) continue;
                        severity = assessment.Severity;
                        break;
                    }
                    result.Add(new MetricSeriesView(pair.Key, pair.Value, severity, profile));
                }
                result.Sort((left, right) =>
                {
                    var comparison = left.Order.CompareTo(right.Order);
                    return comparison != 0
                        ? comparison
                        : string.Compare(left.Label, right.Label, StringComparison.Ordinal);
                });
                return result;
            }
        }

        private sealed class MetricSeriesView
        {
            public MetricSeriesView(
                string fallbackLabel,
                List<BattleDiagnosticMetricAggregate> aggregates,
                BattleDiagnosticMetricSeverity severity,
                BattleDiagnosticResolvedMetricProfile profile)
            {
                Aggregates = aggregates;
                Points = new Vector3[aggregates.Count];
                var first = aggregates[0];
                if (BattleDiagnosticFrameMetricCatalog.TryGet(first.Metric, profile, out var descriptor))
                {
                    Label = string.IsNullOrEmpty(first.Dimension)
                        ? descriptor.DisplayName
                        : descriptor.DisplayName + " [" + first.Dimension + "]";
                    Unit = descriptor.Unit;
                    Order = descriptor.Order;
                }
                else
                {
                    Label = fallbackLabel;
                    Unit = string.Empty;
                    Order = int.MaxValue;
                }
                Severity = severity;
                MinimumValue = first.MinimumValue;
                MaximumValue = first.MaximumValue;
                for (var i = 1; i < aggregates.Count; i++)
                {
                    MinimumValue = Math.Min(MinimumValue, aggregates[i].MinimumValue);
                    MaximumValue = Math.Max(MaximumValue, aggregates[i].MaximumValue);
                }
                LatestValue = aggregates[aggregates.Count - 1].LastValue;
                ScaleMinimum = descriptor.HasSuggestedRange
                    ? Math.Min(MinimumValue, descriptor.SuggestedMinimum)
                    : MinimumValue;
                ScaleMaximum = descriptor.HasSuggestedRange
                    ? Math.Max(MaximumValue, descriptor.SuggestedMaximum)
                    : MaximumValue;
            }

            public string Label { get; }
            public string Unit { get; }
            public int Order { get; }
            public BattleDiagnosticMetricSeverity Severity { get; }
            public List<BattleDiagnosticMetricAggregate> Aggregates { get; }
            public Vector3[] Points { get; }
            public double MinimumValue { get; }
            public double MaximumValue { get; }
            public double LatestValue { get; }
            public double ScaleMinimum { get; }
            public double ScaleMaximum { get; }
        }

        private static BattleDiagnosticFrameRange ResolveRange(in BattleDebugContext ctx)
        {
            var workspace = ctx.WorkspaceState;
            if (workspace != null && workspace.TimeRange.Range.IsValid)
                return workspace.TimeRange.Range;
            if (workspace != null && workspace.FrameCursor.HasFrame)
            {
                var frame = workspace.FrameCursor.Frame;
                return new BattleDiagnosticFrameRange(Math.Max(0, frame - 300), frame + 300);
            }
            return new BattleDiagnosticFrameRange(BattleDiagnosticFrames.Invalid, BattleDiagnosticFrames.Invalid);
        }

        private static void ApplyInteraction(
            in BattleDebugContext ctx,
            in BattleDebugTimelineInteractionResult interaction)
        {
            if (ctx.WorkspaceState == null ||
                !BattleDebugTimelineInteraction.Apply(ctx.WorkspaceState, interaction)) return;
            ctx.RequestRepaint?.Invoke();
        }

        private static Color SeriesColor(int index)
        {
            switch (index % 6)
            {
                case 0: return new Color(0.25f, 0.67f, 0.95f, 1f);
                case 1: return new Color(0.35f, 0.82f, 0.48f, 1f);
                case 2: return new Color(0.96f, 0.68f, 0.25f, 1f);
                case 3: return new Color(0.92f, 0.38f, 0.42f, 1f);
                case 4: return new Color(0.66f, 0.52f, 0.92f, 1f);
                default: return new Color(0.35f, 0.82f, 0.82f, 1f);
            }
        }

        private static Color SeverityColor(BattleDiagnosticMetricSeverity severity)
        {
            return severity == BattleDiagnosticMetricSeverity.Critical
                ? new Color(0.92f, 0.32f, 0.34f, 1f)
                : new Color(0.95f, 0.65f, 0.2f, 1f);
        }
    }
}
