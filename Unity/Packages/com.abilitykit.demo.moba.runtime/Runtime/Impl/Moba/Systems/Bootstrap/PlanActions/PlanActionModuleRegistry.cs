using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed class PlanActionModuleRegistry : IService
    {
        public IPlanActionModule[] Modules { get; }

        public PlanActionModuleRegistry(IPlanActionModule[] modules)
        {
            Modules = modules;
        }

        public static PlanActionModuleRegistry CreateDefault()
        {
            var asm = typeof(PlanActionModuleRegistry).Assembly;
            var list = new List<(int order, string name, IPlanActionModule module)>();

            foreach (var t in asm.GetTypes())
            {
                if (t == null) continue;
                if (t.IsAbstract || t.IsInterface) continue;
                if (!typeof(IPlanActionModule).IsAssignableFrom(t)) continue;

                var attr = t.GetCustomAttribute<PlanActionModuleAttribute>();
                if (attr == null) continue;

                if (t.GetConstructor(Type.EmptyTypes) == null) continue;

                if (Activator.CreateInstance(t) is IPlanActionModule m)
                {
                    list.Add((attr.Order, t.FullName ?? t.Name, m));
                }
            }

            list.Sort(static (a, b) =>
            {
                var c = a.order.CompareTo(b.order);
                if (c != 0) return c;
                return string.CompareOrdinal(a.name, b.name);
            });

            var modules = new IPlanActionModule[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                modules[i] = list[i].module;
            }

            return new PlanActionModuleRegistry(modules);
        }

        public void Dispose()
        {
        }
    }
}
