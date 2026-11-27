using System.Reflection;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class RetiredCompatibilitySurfaceTests
{
    [Fact]
    public void Retired_entry_points_stay_absent_from_the_default_assembly()
    {
        var database = typeof(TriggerPlanJsonDatabase);
        var assembly = database.Assembly;
        var exported = assembly.GetExportedTypes().Select(type => type.FullName).ToHashSet();
        var runnerNamespace = typeof(TriggerRunner<>).Namespace;
        var dispatcherNamespace = typeof(ITriggerDispatcherContext).Namespace;
        string[] retiredTypes =
        [
            $"{dispatcherNamespace}.EDispatcherType",
            $"{dispatcherNamespace}.ITriggerDispatcher",
            $"{dispatcherNamespace}.DispatcherRegistration`1",
            $"{dispatcherNamespace}.TriggerDispatcherBase",
            $"{dispatcherNamespace}.TriggerExecutor`1",
            $"{dispatcherNamespace}.EventBusDispatcherContext",
            $"{runnerNamespace}.HierarchicalTriggerRunner`1",
            $"{runnerNamespace}.HierarchicalOptions",
        ];

        Assert.All(retiredTypes, name => Assert.DoesNotContain(name, exported));
        Assert.DoesNotContain(database.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name == "RegisterAll");
        Assert.Contains(typeof(ITriggerDispatcherContext).FullName, exported);
        Assert.Contains(typeof(TriggerPredicate<>).FullName, exported);
        Assert.Contains(database.FullName, exported);
    }

    [Fact]
    public void Old_schedule_namespaces_are_retired_without_removing_core_scheduling_contracts()
    {
        var exported = typeof(TriggerRunner<>).Assembly.GetExportedTypes();
        var runtimeNamespace = typeof(TriggerRunner<>).Namespace;

        foreach (var retiredNamespace in new[] { $"{runtimeNamespace}.Schedule", $"{runtimeNamespace}.RuleScheduler" })
        {
            Assert.DoesNotContain(exported, type => type.Namespace == retiredNamespace ||
                type.Namespace?.StartsWith(retiredNamespace + ".", StringComparison.Ordinal) == true);
        }
        Assert.DoesNotContain(exported, type => type.Name == "RuleSchedulePlanValidator");
        Assert.Contains(typeof(IFlatActionScheduler), exported);
        Assert.Contains(typeof(TriggerExecutionScheduler), exported);
    }

    [Fact]
    public void Retired_instance_services_do_not_export_types_or_replace_formal_recovery()
    {
        var runtimeNamespace = typeof(TriggerRunner<>).Namespace;
        var exported = typeof(TriggerRunner<>).Assembly.GetExportedTypes().Select(type => type.FullName).ToHashSet();
        string[] retiredTypes =
        [
            $"{runtimeNamespace}.ITriggerRegistry",
            $"{runtimeNamespace}.ITriggerHandle",
            $"{runtimeNamespace}.ITriggerSnapshot",
            $"{runtimeNamespace}.Registry.ITriggerRegistry",
            $"{runtimeNamespace}.Registry.TriggerRegistry",
            $"{runtimeNamespace}.Registry.TriggerRegistryEntry",
        ];

        Assert.All(retiredTypes, name => Assert.DoesNotContain(name, exported));
        Assert.DoesNotContain(exported, name => name?.StartsWith($"{runtimeNamespace}.Instance.", StringComparison.Ordinal) == true ||
            name?.StartsWith($"{runtimeNamespace}.Sync.", StringComparison.Ordinal) == true);
        Assert.Contains(typeof(TriggerRunner<>).FullName, exported);
        Assert.Contains(typeof(TriggerExecutionProgress).FullName, exported);
    }

    [Fact]
    public void Legacy_decorators_and_controller_are_absent_but_executable_wrappers_remain()
    {
        var factory = typeof(ScheduledExecutableFactory);
        var executableNamespace = factory.Namespace;
        var exported = factory.Assembly.GetExportedTypes().Select(type => type.FullName).ToHashSet();
        string[] retiredTypes =
        [
            "DecoratorRegistry", "DecoratorBuilder", "DecoratorImplAttribute",
            "IComposableExecutable", "ComposableExecutableBase", "IDecorator",
            "IDurationDecorator", "ITagDecorator", "IModifierDecorator", "IStackDecorator",
            "IHierarchyDecorator", "IContinuousDecorator", "ICapabilityDecorator",
            "ICapabilityApplier", "ICapabilityContainer", "CapabilityId",
            "ScheduledExecutor", "IScheduleController", "NullScheduleController",
        ];

        Assert.All(retiredTypes, name => Assert.DoesNotContain($"{executableNamespace}.{name}", exported));
        Assert.Null(factory.GetMethod("CreateController"));
        Assert.Contains(typeof(ExecutableExecutor).FullName, exported);
        Assert.Contains(typeof(IScheduledExecutable).FullName, exported);
        Assert.Contains(typeof(TimedExecutable).FullName, exported);
        Assert.Contains(typeof(PeriodicExecutable).FullName, exported);
        Assert.Contains(typeof(ExternalControlledExecutable).FullName, exported);
        Assert.Contains(typeof(TriggerExecutionScheduler).FullName, exported);
    }
}
