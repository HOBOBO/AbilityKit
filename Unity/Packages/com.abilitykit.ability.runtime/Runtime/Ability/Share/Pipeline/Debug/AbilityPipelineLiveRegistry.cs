#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    public static class AbilityPipelineLiveRegistry
    {
        public sealed class Entry
        {
            public readonly string Name;
            public readonly int ConfigId;
            public readonly WeakReference Config;
            public readonly WeakReference Pipeline;

            public Entry(string name, int configId, object config, object pipeline)
            {
                Name = name ?? string.Empty;
                ConfigId = configId;
                Config = new WeakReference(config);
                Pipeline = new WeakReference(pipeline);
            }
        }

        static readonly List<Entry> _entries = new List<Entry>();

        public static event Action Changed;

        public static object SelectedPipeline { get; set; }

        public static void Register(IAbilityPipeline pipeline, IAbilityPipelineConfig config)
        {
            if (pipeline == null) return;

            CleanupDeadEntries();

            var name = config != null
                ? $"{config.ConfigName} ({config.ConfigId})"
                : pipeline.GetType().Name;

            var configId = config != null ? config.ConfigId : 0;

            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (ReferenceEquals(e.Pipeline.Target, pipeline))
                {
                    _entries[i] = new Entry(name, configId, config, pipeline);
                    Changed?.Invoke();
                    return;
                }
            }

            _entries.Add(new Entry(name, configId, config, pipeline));
            if (SelectedPipeline == null) SelectedPipeline = pipeline;
            Changed?.Invoke();
        }

        public static void Unregister(IAbilityPipeline pipeline)
        {
            if (pipeline == null) return;

            var removed = false;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var target = _entries[i].Pipeline.Target;
                if (target == null || ReferenceEquals(target, pipeline))
                {
                    _entries.RemoveAt(i);
                    removed = true;
                }
            }

            if (ReferenceEquals(SelectedPipeline, pipeline))
            {
                SelectedPipeline = null;
            }

            if (removed)
                Changed?.Invoke();
        }

        public static IReadOnlyList<Entry> GetEntries()
        {
            CleanupDeadEntries();
            return _entries;
        }

        static void CleanupDeadEntries()
        {
            var removed = false;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Pipeline.Target == null)
                {
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
