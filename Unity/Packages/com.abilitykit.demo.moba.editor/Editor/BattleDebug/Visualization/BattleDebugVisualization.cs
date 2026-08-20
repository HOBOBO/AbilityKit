using System;
using System.Collections.Generic;
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

            var result = metricSession.QueryMetrics(new BattleDiagnosticMetricQuery(
                1,
                visibleRange,
                new BattleDiagnosticPageRequest(0L, 0, BattleDiagnosticPageRequest.MaximumPageSize),
                category));
            if (!result.Status.CanDisplayResults)
            {
                EditorGUILayout.HelpBox(result.Status.Message, MessageType.Info);
                return true;
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (result.Items.Count == 0)
            {
                EditorGUILayout.HelpBox("No metric samples intersect the shared frame range.", MessageType.Info);
                return true;
            }

            var overviewItems = new List<BattleDebugTimelineOverviewItem>(result.Items.Count);
            for (var i = 0; i < result.Items.Count; i++)
                overviewItems.Add(new BattleDebugTimelineOverviewItem(result.Items[i].Frame, result.Items[i].Frame));
            ApplyInteraction(
                in ctx,
                BattleDebugTimelineOverview.Draw(
                    overviewItems,
                    visibleRange,
                    visibleRange,
                    ctx.WorkspaceState?.FrameCursor.Frame ?? BattleDiagnosticFrames.Invalid,
                    OverviewBuffer));

            var series = BuildSeries(result.Items);
            var drawn = 0;
            foreach (var pair in series)
            {
                if (drawn == MaximumSeries) break;
                DrawSeries(in ctx, pair.Key, pair.Value, visibleRange, drawn++);
            }
            if (series.Count > MaximumSeries)
                EditorGUILayout.LabelField($"Showing {MaximumSeries} of {series.Count} metric series.", EditorStyles.miniLabel);
            if (result.Status.HasMore)
                EditorGUILayout.HelpBox(
                    $"History view is limited to the first {BattleDiagnosticPageRequest.MaximumPageSize} samples in this range.",
                    MessageType.Info);
            EditorGUILayout.Space(4f);
            return true;
        }

        private static Dictionary<string, List<BattleDiagnosticMetricSample>> BuildSeries(
            IReadOnlyList<BattleDiagnosticMetricSample> items)
        {
            var result = new Dictionary<string, List<BattleDiagnosticMetricSample>>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var key = string.IsNullOrEmpty(item.Dimension)
                    ? item.Metric
                    : item.Metric + " [" + item.Dimension + "]";
                if (!result.TryGetValue(key, out var values))
                {
                    values = new List<BattleDiagnosticMetricSample>();
                    result.Add(key, values);
                }
                values.Add(item);
            }
            return result;
        }

        private static void DrawSeries(
            in BattleDebugContext ctx,
            string label,
            IReadOnlyList<BattleDiagnosticMetricSample> samples,
            BattleDiagnosticFrameRange range,
            int colorIndex)
        {
            if (samples.Count == 0) return;
            var min = samples[0].Value;
            var max = min;
            for (var i = 1; i < samples.Count; i++)
            {
                min = Math.Min(min, samples[i].Value);
                max = Math.Max(max, samples[i].Value);
            }

            var rect = EditorGUILayout.GetControlRect(false, 42f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.12f));
            GUI.Label(
                new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f),
                $"{label}  {samples[samples.Count - 1].Value:0.###}  [{min:0.###}, {max:0.###}]",
                EditorStyles.miniLabel);
            var plot = new Rect(rect.x + 2f, rect.y + 18f, rect.width - 4f, 22f);
            if (Event.current.type == EventType.Repaint)
            {
                var points = new Vector3[samples.Count];
                var frameSpan = Math.Max(1L, (long)range.LastFrame - range.FirstFrame);
                var valueSpan = Math.Max(0.000001d, max - min);
                for (var i = 0; i < samples.Count; i++)
                {
                    var x = plot.x + plot.width * (((long)samples[i].Frame - range.FirstFrame) / (float)frameSpan);
                    var y = plot.yMax - plot.height * (float)((samples[i].Value - min) / valueSpan);
                    points[i] = new Vector3(x, y, 0f);
                }
                Handles.BeginGUI();
                Handles.color = SeriesColor(colorIndex);
                if (points.Length == 1) Handles.DrawSolidDisc(points[0], Vector3.forward, 2f);
                else Handles.DrawAAPolyLine(2f, points);
                Handles.EndGUI();
            }

            ApplyInteraction(in ctx, BattleDebugTimelineInteraction.Handle(plot, range));
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
    }
}
