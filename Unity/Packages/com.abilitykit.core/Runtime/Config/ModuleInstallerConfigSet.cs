using System;

namespace AbilityKit.Core.Configuration
{
    [Obsolete("Module installation policy no longer belongs in Core. Move it to the owning bootstrap package; this compatibility API will be removed in the next major version.")]
    public sealed class ModuleInstallerConfigSet
    {
        public ModuleInstallerConfig[] Modules = Array.Empty<ModuleInstallerConfig>();

        public ModuleInstallerConfig? FindModule(string moduleKey)
        {
            var ms = Modules;
            if (ms == null || ms.Length == 0) return null;

            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i];
                if (m == null || !m.IsValid) continue;
                if (string.Equals(m.ModuleKey, moduleKey, StringComparison.Ordinal)) return m;
            }

            return null;
        }
    }
}
