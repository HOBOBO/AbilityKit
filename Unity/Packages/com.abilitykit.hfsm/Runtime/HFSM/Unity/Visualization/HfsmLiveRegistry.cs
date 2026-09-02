#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityHFSM.Inspection;

namespace UnityHFSM.Visualization
{
    public static class HfsmLiveRegistry
    {
        public sealed class Entry
        {
            internal Entry(LiveRegistry.Entry entry)
            {
                Name = entry.Name;
                Fsm = entry.Fsm;
                FsmType = entry.FsmType;
            }

            public readonly string Name;
            public readonly WeakReference Fsm;
            public readonly Type FsmType;
        }

        static HfsmLiveRegistry()
        {
            RuntimeInspectionRegistryInstaller.EnsureInstalled();
        }

        public static bool AutoRegisterEnabled
        {
            get => LiveRegistry.AutoRegisterEnabled;
            set => LiveRegistry.AutoRegisterEnabled = value;
        }

        public static Predicate<object> AutoRegisterFilter
        {
            get => LiveRegistry.AutoRegisterFilter;
            set => LiveRegistry.AutoRegisterFilter = value;
        }

        public static Func<object, string> AutoRegisterNameProvider
        {
            get => LiveRegistry.AutoRegisterNameProvider;
            set => LiveRegistry.AutoRegisterNameProvider = value;
        }

        public static event Action Changed
        {
            add => LiveRegistry.Changed += value;
            remove => LiveRegistry.Changed -= value;
        }

        public static void AutoRegister(object fsm) => LiveRegistry.AutoRegister(fsm);
        public static void Register(string name, object fsm) => LiveRegistry.Register(name, fsm);
        public static void Unregister(object fsm) => LiveRegistry.Unregister(fsm);

        public static IReadOnlyList<Entry> GetEntries()
        {
            var source = LiveRegistry.GetEntries();
            var entries = new Entry[source.Count];
            for (var index = 0; index < source.Count; index++)
                entries[index] = new Entry(source[index]);
            return entries;
        }
    }

    [InitializeOnLoad]
    internal static class RuntimeInspectionRegistryInstaller
    {
        private static IDisposable _installation;

        static RuntimeInspectionRegistryInstaller()
        {
            EnsureInstalled();
        }

        internal static void EnsureInstalled()
        {
            if (RuntimeInspectionHub.Backend is UnityLiveRegistryBackend)
                return;
            _installation?.Dispose();
            _installation = RuntimeInspectionHub.InstallBackend(new UnityLiveRegistryBackend());
        }

        private sealed class UnityLiveRegistryBackend : IRuntimeInspectionRegistryBackend
        {
            public void AutoRegister(object runtime) => LiveRegistry.AutoRegister(runtime);
            public void Unregister(object runtime) => LiveRegistry.Unregister(runtime);
        }
    }
}

#endif
