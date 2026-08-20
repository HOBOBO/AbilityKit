using System;
using System.Collections;
using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Diagnostics;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaPerformanceProfilingTests
{
    [Fact]
    public void Trigger_and_effect_markers_build_a_nested_flame_tree()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        ProfilerHub.SetProfiler(profiler);

        try
        {
            var diagnostics = new MobaBattleDiagnosticsService();
            var adapter = new MobaTriggerDiagnosticsAdapter(new TestWorldResolver(diagnostics));
            var key = new EventKey<int>(1001);
            var args = 42;

            adapter.OnEventDispatching(key, in args);
            var traceScope = adapter.BeginTrace(key, in args);

            adapter.OnBeforeEvaluate(key, in args, 0, 0, 1L);
            adapter.OnAfterEvaluate(key, in args, 0, 0, 1L, true);

            adapter.OnActionExecuting(key, in args, 0, 0, 1L, 0, "TestAction", 0, 1);
            var effectScope = MobaPerformanceProfiling.Begin(
                diagnostics,
                MobaBattleDiagnosticChannel.TriggerHook,
                MobaBattleDiagnosticMetric.EffectExecuteScope);
            var effectActionScope = MobaPerformanceProfiling.Begin(
                diagnostics,
                MobaBattleDiagnosticChannel.TriggerHook,
                MobaBattleDiagnosticMetric.EffectActionScope);
            effectActionScope.Dispose();
            effectScope.Dispose();
            adapter.OnActionExecuted(key, in args, 0, 0, 1L, 0, "TestAction", 0, 1, false);

            adapter.EndTrace(traceScope);
            adapter.OnEventDispatched(key, in args, 1, 0);

            var root = profiler.GetRoot().Roots["moba"];
            var dispatch = root.Children[MobaBattleDiagnosticMetric.TriggerDispatchScope];
            Assert.True(dispatch.Children.ContainsKey(MobaBattleDiagnosticMetric.TriggerEvaluateScope));

            var execute = dispatch.Children[MobaBattleDiagnosticMetric.TriggerExecuteScope];
            var effect = execute.Children[MobaBattleDiagnosticMetric.EffectExecuteScope];
            Assert.True(effect.Children.ContainsKey(MobaBattleDiagnosticMetric.EffectActionScope));
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
            profiler.Stop();
        }
    }

    [Fact]
    public void Disabled_trigger_channel_does_not_create_performance_nodes()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        ProfilerHub.SetProfiler(profiler);

        try
        {
            var diagnostics = new MobaBattleDiagnosticsService();
            diagnostics.SetChannelEnabled(MobaBattleDiagnosticChannel.TriggerHook, false);
            var adapter = new MobaTriggerDiagnosticsAdapter(new TestWorldResolver(diagnostics));
            var key = new EventKey<int>(1002);
            var args = 7;

            var traceScope = adapter.BeginTrace(key, in args);
            adapter.OnBeforeEvaluate(key, in args, 0, 0, 1L);
            adapter.OnAfterEvaluate(key, in args, 0, 0, 1L, true);
            adapter.OnActionExecuting(key, in args, 0, 0, 1L, 0, "TestAction", 0, 1);
            adapter.OnActionExecuted(key, in args, 0, 0, 1L, 0, "TestAction", 0, 1, false);
            adapter.EndTrace(traceScope);

            Assert.Empty(profiler.GetRoot().Roots);
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
            profiler.Stop();
        }
    }

    [Fact]
    public void Disabled_profiler_does_not_allocate_trigger_scope_stacks()
    {
        ProfilerHub.SetProfiler(null);
        var diagnostics = new MobaBattleDiagnosticsService();
        var adapter = new MobaTriggerDiagnosticsAdapter(new TestWorldResolver(diagnostics));
        var key = new EventKey<int>(1004);
        var args = 11;

        var traceScope = adapter.BeginTrace(key, in args);
        adapter.OnBeforeEvaluate(key, in args, 0, 0, 1L);
        adapter.OnAfterEvaluate(key, in args, 0, 0, 1L, true);
        adapter.OnActionExecuting(key, in args, 0, 0, 1L, 0, "TestAction", 0, 1);
        adapter.OnActionExecuted(key, in args, 0, 0, 1L, 0, "TestAction", 0, 1, false);
        adapter.EndTrace(traceScope);

        Assert.Null(GetPrivateCollection(adapter, "_dispatchScopes"));
        Assert.Null(GetPrivateCollection(adapter, "_evaluateScopes"));
        Assert.Null(GetPrivateCollection(adapter, "_executeScopes"));
    }

    [Fact]
    public void Ending_dispatch_closes_incomplete_nested_scopes()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        ProfilerHub.SetProfiler(profiler);

        try
        {
            var diagnostics = new MobaBattleDiagnosticsService();
            var adapter = new MobaTriggerDiagnosticsAdapter(new TestWorldResolver(diagnostics));
            var key = new EventKey<int>(1003);
            var args = 9;

            var traceScope = adapter.BeginTrace(key, in args);
            adapter.OnBeforeEvaluate(key, in args, 0, 0, 1L);
            adapter.EndTrace(traceScope);

            var afterDispatch = ProfilerHub.Begin("moba.test.after-dispatch").ToScope();
            afterDispatch.Dispose();

            var root = profiler.GetRoot().Roots["moba"];
            var dispatch = root.Children[MobaBattleDiagnosticMetric.TriggerDispatchScope];
            Assert.False(dispatch.Children.ContainsKey("moba.test.after-dispatch"));
            Assert.True(root.Children.ContainsKey("moba.test.after-dispatch"));
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
            profiler.Stop();
        }
    }

    [Fact]
    public void Effect_service_builds_effect_and_action_flame_scopes()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        ProfilerHub.SetProfiler(profiler);

        try
        {
            var trace = new MobaTraceRegistry();
            var diagnostics = new MobaBattleDiagnosticsService();
            var service = new MobaEffectExecutionService();
            SetMember(service, "Trace", trace);
            SetMember(service, "_diagnostics", diagnostics);

            var lineage = new MobaEffectLineageInput(
                EffectContextKind.Skill,
                MobaTraceKind.SkillEffect,
                7,
                9,
                0L,
                0L,
                0L,
                801);
            InvokePrivate(service, "BeginEffectTraceScope", 801, 802, lineage);
            service.EnterActionExecution(0, 901L);
            service.ExitActionExecution(0, 901L, true);
            InvokePrivate(service, "EndCurrentTrace", (int)TraceLifecycleReason.Completed);

            var root = profiler.GetRoot().Roots["moba"];
            var effect = root.Children[MobaBattleDiagnosticMetric.EffectExecuteScope];
            Assert.True(effect.Children.ContainsKey(MobaBattleDiagnosticMetric.EffectActionScope));
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
            profiler.Stop();
        }
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(target, args);
    }

    private static void SetMember(object target, string memberName, object value)
    {
        var property = target.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            property.SetValue(target, value);
            return;
        }

        var field = target.GetType().GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private static ICollection GetPrivateCollection(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field.GetValue(target) as ICollection;
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly IMobaBattleDiagnosticsService _diagnostics;

        public TestWorldResolver(IMobaBattleDiagnosticsService diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public object Resolve(Type serviceType) =>
            serviceType == typeof(IMobaBattleDiagnosticsService) ? _diagnostics : null;

        public T Resolve<T>() => TryResolve<T>(out var instance) ? instance : default;

        public bool TryResolve(Type serviceType, out object instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }

        public bool TryResolve<T>(out T instance)
        {
            if (_diagnostics is T resolved)
            {
                instance = resolved;
                return true;
            }

            instance = default;
            return false;
        }
    }
}
