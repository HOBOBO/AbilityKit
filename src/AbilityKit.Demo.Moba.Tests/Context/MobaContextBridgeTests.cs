using System.Linq;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Context;

public sealed class MobaContextBridgeTests
{
    [Fact]
    public void Context_flow_can_bind_moba_trace_identity_and_query_entities()
    {
        var registry = new ContextRegistry();
        var observedEvents = new List<ContextEvent>();
        registry.Subscribe(observedEvents.Add);

        using var flow = registry.BeginFlow("moba-skill-effect");
        var tracedEntityId = flow.Create()
            .WithTrace(rootTraceId: 10, traceContextId: 11, traceKind: (int)MobaExecutionKind.SkillEffect)
            .Build();
        var plainEntityId = flow.CreateEntity();

        Assert.True(registry.Exists(tracedEntityId));
        Assert.True(registry.Exists(plainEntityId));
        Assert.Equal(flow.FlowId, registry.GetEntityFlow(tracedEntityId));
        Assert.Equal(flow.FlowId, registry.GetEntityFlow(plainEntityId));
        Assert.True(registry.TryGetTrace(tracedEntityId, out var trace));
        Assert.Equal(10, trace.RootTraceId);
        Assert.Equal(11, trace.TraceContextId);
        Assert.Equal((int)MobaExecutionKind.SkillEffect, trace.TraceKind);
        Assert.Equal(new[] { tracedEntityId }, registry.GetEntitiesByTraceRoot(10));
        Assert.Equal(new[] { tracedEntityId }, registry.GetEntitiesByTraceContext(11));
        Assert.Equal(new[] { tracedEntityId }, registry.GetEntitiesByTraceKind((int)MobaExecutionKind.SkillEffect));

        var tracedEntities = registry.Query()
            .CreateQuery()
            .With<TraceContextProperty>()
            .Execute()
            .ToArray();
        var untracedEntities = registry.Query()
            .CreateQuery()
            .Without<TraceContextProperty>()
            .Execute()
            .ToArray();

        Assert.Equal(new[] { tracedEntityId }, tracedEntities);
        Assert.Equal(new[] { plainEntityId }, untracedEntities);
        Assert.Contains(tracedEntityId, registry.GetEntitiesInFlow(flow.FlowId));
        Assert.Contains(plainEntityId, registry.GetEntitiesInFlow(flow.FlowId));
        Assert.Contains(observedEvents, item => item.Type == ContextEventType.FlowCreated && item.FlowId == flow.FlowId);
        Assert.Contains(observedEvents, item => item.Type == ContextEventType.Created && item.EntityId == tracedEntityId);
    }

    [Fact]
    public void Business_properties_and_typed_predicates_can_filter_context_entities()
    {
        var registry = new ContextRegistry();

        using var flow = registry.BeginFlow("moba-buff-tick");
        var skillEntityId = flow.Create()
            .WithTrace(rootTraceId: 100, traceContextId: 101, traceKind: (int)MobaExecutionKind.SkillEffect)
            .With(new MobaContextCategoryProperty("skill"))
            .Build();
        var buffEntityId = flow.Create()
            .WithTrace(rootTraceId: 100, traceContextId: 102, traceKind: (int)MobaExecutionKind.BuffTick)
            .With(new MobaContextCategoryProperty("buff"))
            .Build();
        _ = flow.CreateEntity();

        var buffTraceEntities = registry.Query()
            .CreateQuery()
            .With<TraceContextProperty>()
            .Where<TraceContextProperty>((_, trace) => trace.TraceKind == (int)MobaExecutionKind.BuffTick)
            .Execute()
            .ToArray();
        var businessCategoryEntities = registry.Query()
            .CreateQuery()
            .With<MobaContextCategoryProperty>()
            .Where<MobaContextCategoryProperty>((_, category) => category.Category == "buff")
            .Execute()
            .ToArray();
        var evenTraceContextEntities = registry.Query()
            .CreateQuery()
            .With<TraceContextProperty>()
            .Where(entityId => registry.Get<TraceContextProperty>(entityId).TraceContextId % 2 == 0)
            .Execute()
            .ToArray();

        Assert.Equal(new[] { buffEntityId }, buffTraceEntities);
        Assert.Equal(new[] { buffEntityId }, businessCategoryEntities);
        Assert.Equal(new[] { buffEntityId }, evenTraceContextEntities);
        Assert.NotEqual(skillEntityId, buffEntityId);
    }

    [Fact]
    public void Registry_bound_typed_predicates_use_execution_registry()
    {
        var registry = new ContextRegistry();

        using var flow = registry.BeginFlow("typed-query-registry");
        _ = flow.Create()
            .WithTrace(rootTraceId: 200, traceContextId: 201, traceKind: (int)MobaExecutionKind.SkillEffect)
            .Build();
        var buffEntityId = flow.Create()
            .WithTrace(rootTraceId: 200, traceContextId: 202, traceKind: (int)MobaExecutionKind.BuffTick)
            .Build();

        var result = registry.Query()
            .CreateQuery()
            .With<TraceContextProperty>()
            .Where<TraceContextProperty>((_, trace) => trace.TraceKind == (int)MobaExecutionKind.BuffTick)
            .Execute(registry)
            .ToArray();

        Assert.Equal(new[] { buffEntityId }, result);
    }

    private sealed class MobaContextCategoryProperty : IProperty
    {
        public MobaContextCategoryProperty(string category)
        {
            Category = category;
        }

        public int TypeId => PropertyTypeRegistry.Instance.Register<MobaContextCategoryProperty>().Id;
        public string Category { get; }
    }
}
