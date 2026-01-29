#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    public static class AbilityPipelineLiveRegistry
    {
        public struct Snapshot
        {
            public EAbilityPipelineState State;
            public AbilityPipelinePhaseId CurrentPhaseId;
            public bool IsPaused;
            public int PhaseIndex;
        }

        public sealed class Entry
        {
            public readonly string Name;
            public readonly int ConfigId;
            public readonly WeakReference Config;
            public readonly WeakReference Pipeline;
            public readonly WeakReference Run;

#if UNITY_EDITOR
            public readonly PipelineRunTrace Trace;
#endif

            public Snapshot LastSnapshot;

            public Entry(string name, int configId, object config, object pipeline, object run, Snapshot snapshot, PipelineRunTrace trace)
            {
                Name = name ?? string.Empty;
                ConfigId = configId;
                Config = new WeakReference(config);
                Pipeline = new WeakReference(pipeline);
                Run = new WeakReference(run);
                LastSnapshot = snapshot;

#if UNITY_EDITOR
                Trace = trace;
#endif
            }
        }

        static readonly List<Entry> _entries = new List<Entry>();

        public static event Action Changed;

        public static object SelectedRun { get; set; }

        public static void RegisterRun(object pipeline, IAbilityPipelineConfig config, object run)
        {
            if (pipeline == null) return;
            if (run == null) return;

            CleanupDeadEntries();

            var name = config != null
                ? $"{config.ConfigName} ({config.ConfigId})"
                : pipeline.GetType().Name;

            var configId = config != null ? config.ConfigId : 0;

            var snapshot = CaptureSnapshot(run);

            var trace = new PipelineRunTrace(capacity: 2048);

            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (ReferenceEquals(e.Run.Target, run))
                {
                    _entries[i] = new Entry(name, configId, config, pipeline, run, snapshot, trace);
                    Changed?.Invoke();
                    return;
                }
            }

            _entries.Add(new Entry(name, configId, config, pipeline, run, snapshot, trace));
            if (SelectedRun == null) SelectedRun = run;
            Changed?.Invoke();
        }

        public static void UnregisterRun(object run)
        {
            if (run == null) return;

            var removed = false;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                var target = e.Run.Target;
                if (target == null || ReferenceEquals(target, run))
                {
#if UNITY_EDITOR
                    try
                    {
                        var s = e.LastSnapshot;
                        e.Trace?.Add(PipelineTraceEventType.RunEnd, s.CurrentPhaseId, s.State, "Unregister");
                    }
                    catch
                    {
                    }
#endif
                    _entries.RemoveAt(i);
                    removed = true;
                }
            }

            if (ReferenceEquals(SelectedRun, run))
            {
                SelectedRun = null;
            }

            if (removed)
                Changed?.Invoke();
        }

        public static IReadOnlyList<Entry> GetEntries()
        {
            CleanupDeadEntries();
            return _entries;
        }

        public static bool TryGetSnapshot(object run, out Snapshot snapshot)
        {
            snapshot = default;
            if (run == null) return false;
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!ReferenceEquals(e.Run.Target, run)) continue;
                snapshot = e.LastSnapshot;
                return true;
            }
            return false;
        }

        public static bool TryGetTrace(object run, out PipelineRunTrace trace)
        {
            trace = null;
            if (run == null) return false;
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!ReferenceEquals(e.Run.Target, run)) continue;
                trace = e.Trace;
                return trace != null;
            }
            return false;
        }

        public static void TouchRun(object run)
        {
            if (run == null) return;
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!ReferenceEquals(e.Run.Target, run)) continue;
                e.LastSnapshot = CaptureSnapshot(run);

                try
                {
                    var s = e.LastSnapshot;
                    e.Trace?.Add(PipelineTraceEventType.Tick, s.CurrentPhaseId, s.State, string.Empty);
                }
                catch
                {
                }

                Changed?.Invoke();
                return;
            }
        }

        private static Snapshot CaptureSnapshot(object run)
        {
            var s = new Snapshot
            {
                State = EAbilityPipelineState.Ready,
                CurrentPhaseId = default,
                IsPaused = false,
                PhaseIndex = 0,
            };

            if (run == null) return s;

            try
            {
                var t = run.GetType();

                var pState = t.GetProperty("State");
                if (pState != null)
                {
                    var v = pState.GetValue(run);
                    if (v is EAbilityPipelineState es) s.State = es;
                }

                var pPhase = t.GetProperty("CurrentPhaseId");
                if (pPhase != null)
                {
                    var v = pPhase.GetValue(run);
                    if (v is AbilityPipelinePhaseId pid) s.CurrentPhaseId = pid;
                }

                var pPaused = t.GetProperty("IsPaused");
                if (pPaused != null)
                {
                    var v = pPaused.GetValue(run);
                    if (v is bool b) s.IsPaused = b;
                }

                var fIdx = t.GetField("_currentPhaseIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fIdx != null)
                {
                    var v = fIdx.GetValue(run);
                    if (v is int i) s.PhaseIndex = i;
                }
            }
            catch
            {
            }

            return s;
        }

        static void CleanupDeadEntries()
        {
            var removed = false;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.Run.Target == null)
                {
#if UNITY_EDITOR
                    try
                    {
                        var s = e.LastSnapshot;
                        e.Trace?.Add(PipelineTraceEventType.RunEnd, s.CurrentPhaseId, s.State, "GC");
                    }
                    catch
                    {
                    }
#endif
                    _entries.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
                Changed?.Invoke();
        }
    }
}

#endif
