using System.Reflection;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class OptionalCompatibilitySurfaceTests
{
    [Fact]
    public void Optional_entry_points_can_be_excluded_without_removing_required_types()
    {
#if TRIGGERING_OPTIONAL_SOURCE_PROBE
        const bool optionalIncluded = false;
#else
        const bool optionalIncluded = true;
#endif
        var database = typeof(TriggerPlanJsonDatabase);
        var assembly = database.Assembly;
        var exported = assembly.GetExportedTypes().Select(type => type.FullName).ToHashSet();
        var dispatcherNamespace = typeof(ITriggerDispatcherContext).Namespace;
        var schedulerNamespace = typeof(QueuedActionExecutor).Namespace;
        string[] optionalTypes =
        [
            $"{dispatcherNamespace}.EDispatcherType",
            $"{dispatcherNamespace}.ITriggerDispatcher",
            $"{dispatcherNamespace}.DispatcherRegistration`1",
            $"{dispatcherNamespace}.TriggerDispatcherBase",
            $"{dispatcherNamespace}.TriggerExecutor`1",
            $"{dispatcherNamespace}.EventBusDispatcherContext",
            $"{schedulerNamespace}.TriggerMutex",
            $"{schedulerNamespace}.TriggerWaitHandle",
            $"{schedulerNamespace}.SynchronizedActionExecutor",
        ];

        Assert.All(optionalTypes, name => Assert.Equal(optionalIncluded, exported.Contains(name)));
        Assert.Equal(optionalIncluded ? 2 : 0, database.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(method => method.Name == "RegisterAll"));
        Assert.Contains(typeof(QueuedActionExecutor).FullName, exported);
        Assert.Contains(typeof(ITriggerDispatcherContext).FullName, exported);
        Assert.Contains(database.FullName, exported);
    }
}
