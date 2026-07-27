using AbilityKit.Ability.World.Diagnostics;

namespace AbilityKit.Ability.World.DI
{
    public static class WorldCompositionReportBuilder
    {
        public static WorldCompositionReport Create(
            string worldId,
            string worldType,
            WorldModulePlan modulePlan,
            WorldContainerBuilder containerBuilder)
        {
            var report = new WorldCompositionReport(worldId, worldType);
            if (modulePlan != null)
            {
                for (var i = 0; i < modulePlan.Entries.Count; i++)
                {
                    var entry = modulePlan.Entries[i];
                    report.AddModule(new WorldCompositionReport.ModuleEntry(
                        i,
                        entry.SourceIndex,
                        entry.Order,
                        entry.Id,
                        entry.ModuleType.FullName));
                }
            }

            if (containerBuilder != null)
            {
                for (var i = 0; i < containerBuilder.Registrations.Count; i++)
                {
                    var registration = containerBuilder.Registrations[i];
                    report.AddServiceRegistration(
                        new WorldCompositionReport.ServiceRegistrationEntry(
                            registration.Sequence,
                            registration.ServiceType.FullName,
                            registration.ImplementationType.FullName,
                            registration.Lifetime.ToString(),
                            registration.Ownership.ToString(),
                            registration.Policy.ToString(),
                            registration.Outcome.ToString(),
                            registration.SourceModuleType?.FullName,
                            registration.PreviousImplementationType?.FullName,
                            registration.PreviousOwnership?.ToString(),
                            registration.PreviousSourceModuleType?.FullName));
                }
            }

            return report;
        }
    }
}
