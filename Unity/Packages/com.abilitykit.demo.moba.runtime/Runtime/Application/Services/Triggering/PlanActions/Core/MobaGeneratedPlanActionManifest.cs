using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    internal static partial class MobaGeneratedPlanActionManifest
    {
        public static MobaPlanActionDescriptor[] CreateDescriptors()
        {
            var descriptors = new List<MobaPlanActionDescriptor>();
            AddGenerated(descriptors);
            return descriptors.Count == 0
                ? Array.Empty<MobaPlanActionDescriptor>()
                : descriptors.ToArray();
        }

        static partial void AddGenerated(List<MobaPlanActionDescriptor> descriptors);

        private static void Add<TModule>(List<MobaPlanActionDescriptor> descriptors, int order)
            where TModule : IPlanActionModule, IMobaPlanActionMetadata, new()
        {
            var module = new TModule();
            var moduleType = typeof(TModule);
            descriptors.Add(new MobaPlanActionDescriptor(
                order,
                moduleType.FullName ?? moduleType.Name,
                module.ActionName,
                module));
        }
    }
}
