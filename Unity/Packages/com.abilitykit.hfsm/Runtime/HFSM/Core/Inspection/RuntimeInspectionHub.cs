using System;

namespace AbilityKit.HFSM.Inspection
{
    public interface IRuntimeInspectionRegistryBackend
    {
        void AutoRegister(object runtime);
        void Unregister(object runtime);
    }

    public static class RuntimeInspectionHub
    {
        private static readonly object SyncRoot = new object();
        private static IRuntimeInspectionRegistryBackend _backend;

        public static IRuntimeInspectionRegistryBackend Backend
        {
            get
            {
                lock (SyncRoot)
                    return _backend;
            }
        }

        public static IDisposable InstallBackend(IRuntimeInspectionRegistryBackend backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            lock (SyncRoot)
            {
                _backend = backend;
                return new BackendInstallation(backend);
            }
        }

        public static void AutoRegister(object runtime)
        {
            IRuntimeInspectionRegistryBackend backend;
            lock (SyncRoot)
                backend = _backend;
            backend?.AutoRegister(runtime);
        }

        public static void Unregister(object runtime)
        {
            IRuntimeInspectionRegistryBackend backend;
            lock (SyncRoot)
                backend = _backend;
            backend?.Unregister(runtime);
        }

        private sealed class BackendInstallation : IDisposable
        {
            private IRuntimeInspectionRegistryBackend _installedBackend;

            public BackendInstallation(IRuntimeInspectionRegistryBackend installedBackend)
            {
                _installedBackend = installedBackend;
            }

            public void Dispose()
            {
                lock (SyncRoot)
                {
                    if (ReferenceEquals(_backend, _installedBackend))
                        _backend = null;
                    _installedBackend = null;
                }
            }
        }
    }
}
