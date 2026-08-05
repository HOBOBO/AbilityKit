using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using IPlanActionModule = AbilityKit.Triggering.Runtime.Plan.IPlanActionModule;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [WorldService(typeof(PlanActionModuleRegistry), isDefault: false)]
    public sealed class PlanActionModuleRegistry : IService
    {
        public IPlanActionModule[] Modules { get; }
        public MobaPlanActionDescriptor[] Descriptors { get; }

        // 供 DI 容器使用的无参构造函数，内部会自动发现模块。
        public PlanActionModuleRegistry()
        {
            Descriptors = CreateDescriptors();
            Modules = ExtractModules(Descriptors);
        }

        // 供手动创建时显式传入指定模块的构造函数。
        public PlanActionModuleRegistry(IPlanActionModule[] modules)
        {
            Modules = modules ?? Array.Empty<IPlanActionModule>();
            Descriptors = CreateDescriptors(Modules);
        }

        private static MobaPlanActionDescriptor[] CreateDescriptors()
        {
            var generated = MobaGeneratedPlanActionManifest.CreateDescriptors();
            if (generated.Length > 0)
            {
                ValidateDescriptors(generated, "generated manifest");
                return generated;
            }

            if (AppContext.TryGetSwitch(
                    "AbilityKit.Moba.DisablePlanActionReflectionFallback",
                    out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
            {
                throw new InvalidOperationException(
                    "The generated MOBA plan action manifest is empty and reflection fallback is disabled.");
            }

            var reflected = CreateDescriptorsByReflection();
            ValidateDescriptors(reflected, "reflection fallback");
            return reflected;
        }

        private static MobaPlanActionDescriptor[] CreateDescriptorsByReflection()
        {
            var asm = typeof(PlanActionModuleRegistry).Assembly;
            var list = new List<(int order, string name, string actionName, IPlanActionModule module)>();

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
                    list.Add((attr.Order, t.FullName ?? t.Name, GetActionName(m), m));
                }
            }

            list.Sort(static (a, b) =>
            {
                var c = a.order.CompareTo(b.order);
                if (c != 0) return c;
                return string.CompareOrdinal(a.name, b.name);
            });

            var descriptors = new MobaPlanActionDescriptor[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                descriptors[i] = new MobaPlanActionDescriptor(list[i].order, list[i].name, list[i].actionName, list[i].module);
            }

            return descriptors;
        }

        private static void ValidateDescriptors(MobaPlanActionDescriptor[] descriptors, string source)
        {
            var actionNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < descriptors.Length; i++)
            {
                var descriptor = descriptors[i];
                if (descriptor.Module == null)
                {
                    throw new InvalidOperationException(
                        $"MOBA plan action descriptor '{descriptor.ModuleName}' from {source} has no module instance.");
                }

                if (string.IsNullOrWhiteSpace(descriptor.ActionName))
                {
                    throw new InvalidOperationException(
                        $"MOBA plan action module '{descriptor.ModuleName}' from {source} has an empty action name.");
                }

                if (!actionNames.Add(descriptor.ActionName))
                {
                    throw new InvalidOperationException(
                        $"Duplicate MOBA plan action name '{descriptor.ActionName}' in {source}.");
                }
            }
        }

        private static MobaPlanActionDescriptor[] CreateDescriptors(IPlanActionModule[] modules)
        {
            if (modules == null || modules.Length == 0) return Array.Empty<MobaPlanActionDescriptor>();

            var descriptors = new MobaPlanActionDescriptor[modules.Length];
            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                descriptors[i] = new MobaPlanActionDescriptor(0, module?.GetType().FullName ?? string.Empty, GetActionName(module), module);
            }

            return descriptors;
        }

        private static IPlanActionModule[] ExtractModules(MobaPlanActionDescriptor[] descriptors)
        {
            if (descriptors == null || descriptors.Length == 0) return Array.Empty<IPlanActionModule>();

            var modules = new IPlanActionModule[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
            {
                modules[i] = descriptors[i].Module;
            }

            return modules;
        }

        private static string GetActionName(IPlanActionModule module)
        {
            return module is IMobaPlanActionMetadata metadata ? metadata.ActionName : null;
        }

        public static PlanActionModuleRegistry CreateDefault()
        {
            return new PlanActionModuleRegistry();
        }

        public void Dispose()
        {
        }
    }
}
