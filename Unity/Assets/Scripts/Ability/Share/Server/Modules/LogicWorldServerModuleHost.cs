using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Server.Modules
{
    public sealed class LogicWorldServerModuleHost
    {
        private readonly List<ILogicWorldServerModule> _modules = new List<ILogicWorldServerModule>(8);

        public LogicWorldServerModuleHost Add(ILogicWorldServerModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            _modules.Add(module);
            return this;
        }

        public void InstallAll(LogicWorldServerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.Install(options);
            }
        }

        public void UninstallAll(LogicWorldServerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i]?.Uninstall(options);
            }
        }
    }
}
