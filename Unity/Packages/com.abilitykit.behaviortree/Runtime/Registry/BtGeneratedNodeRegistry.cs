using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public interface IBtNodeRegistryModule
    {
        void Register(BtNodeRegistry registry);
    }

    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public static class BtGeneratedNodeRegistry
    {
        private static readonly object Gate = new();
        private static readonly List<IBtNodeRegistryModule> Modules = new();

        public static void RegisterModule(IBtNodeRegistryModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            lock (Gate)
            {
                Modules.Add(module);
            }
        }

        public static int ApplyTo(BtNodeRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            IBtNodeRegistryModule[] modules;
            lock (Gate)
            {
                modules = Modules.ToArray();
            }
            foreach (var module in modules)
            {
                module.Register(registry);
            }
            return modules.Length;
        }

        public static void ClearForTests()
        {
            lock (Gate)
            {
                Modules.Clear();
            }
        }
    }
}
