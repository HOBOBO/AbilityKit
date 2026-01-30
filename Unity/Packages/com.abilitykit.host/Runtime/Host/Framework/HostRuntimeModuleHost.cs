using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Host.Framework
{
    public sealed class HostRuntimeModuleHost
    {
        private readonly List<IHostRuntimeModule> _modules = new List<IHostRuntimeModule>(8);

        public HostRuntimeModuleHost Add(IHostRuntimeModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            _modules.Add(module);
            return this;
        }

        public void InstallAll(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.Install(runtime, options);
            }
        }

        public void UninstallAll(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i]?.Uninstall(runtime, options);
            }
        }
    }
}
