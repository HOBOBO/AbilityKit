using System.Collections.Generic;
using System.Linq;
using AbilityKit.Context;

namespace AbilityKit.Trace
{
    public sealed class TraceContextProperty : IProperty
    {
        public TraceContextProperty(long rootTraceId, long traceContextId, int traceKind = 0)
        {
            RootTraceId = rootTraceId;
            TraceContextId = traceContextId;
            TraceKind = traceKind;
        }

        public int TypeId => PropertyTypeRegistry.Instance.Register<TraceContextProperty>().Id;
        public long RootTraceId { get; }
        public long TraceContextId { get; }
        public int TraceKind { get; }
    }

    public static class TraceContextExtensions
    {
        public static EntityBuilder WithTrace(
            this EntityBuilder builder,
            long rootTraceId,
            long traceContextId,
            int traceKind = 0)
        {
            return builder.With(new TraceContextProperty(rootTraceId, traceContextId, traceKind));
        }

        public static bool TryGetTrace(
            this ContextRegistry registry,
            long entityId,
            out TraceContextProperty trace)
        {
            trace = registry.Get<TraceContextProperty>(entityId);
            return trace != null;
        }

        public static IReadOnlyList<long> GetEntitiesByTraceRoot(
            this ContextRegistry registry,
            long rootTraceId)
        {
            return registry.Query()
                .CreateQuery()
                .With<TraceContextProperty>()
                .Where<TraceContextProperty>((_, trace) => trace.RootTraceId == rootTraceId)
                .Execute()
                .ToArray();
        }

        public static IReadOnlyList<long> GetEntitiesByTraceContext(
            this ContextRegistry registry,
            long traceContextId)
        {
            return registry.Query()
                .CreateQuery()
                .With<TraceContextProperty>()
                .Where<TraceContextProperty>((_, trace) => trace.TraceContextId == traceContextId)
                .Execute()
                .ToArray();
        }

        public static IReadOnlyList<long> GetEntitiesByTraceKind(
            this ContextRegistry registry,
            int traceKind)
        {
            return registry.Query()
                .CreateQuery()
                .With<TraceContextProperty>()
                .Where<TraceContextProperty>((_, trace) => trace.TraceKind == traceKind)
                .Execute()
                .ToArray();
        }
    }
}
