/*
 * 固定弃用工具的实际失败边界，供下一主版本前的兼容维护与迁移使用。
 */
#pragma warning disable CS0618
using System.Reflection;
using AbilityKit.Core.Reflection;
using AbilityKit.Core.Utilities;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class ReflectionAndDisposalCompatibilityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AbilityKit.Tests.MissingReflectionCompatibilityType")]
    public void Type_lookup_returns_null_for_empty_or_unknown_names(string? name)
    {
        Assert.Null(ReflectionInvokeUtils.FindType(name!));
    }

    [Fact]
    public void Type_lookup_finds_loaded_nested_type_and_respects_case()
    {
        var target = typeof(ReflectionTarget);
        Assert.Same(target, ReflectionInvokeUtils.FindType(target.FullName!));
        Assert.Same(target, ReflectionInvokeUtils.FindType(target.AssemblyQualifiedName!));
        Assert.Null(ReflectionInvokeUtils.FindType(target.FullName!.ToLowerInvariant()));
    }

    [Fact]
    public void Type_lookup_preserves_closed_generic_runtime_resolution()
    {
        var type = typeof(Dictionary<string, int>);
        Assert.Same(type, ReflectionInvokeUtils.FindType(type.AssemblyQualifiedName!));
    }

    [Fact]
    public void Initial_type_resolution_failure_escapes_lookup_but_is_caught_by_invocation()
    {
        var assemblyName = "AbilityKit.Compatibility.ResolveFailure." + Guid.NewGuid().ToString("N");
        var typeName = "Missing.Type, " + assemblyName;
        var original = new InvalidOperationException("程序集解析失败");
        ResolveEventHandler handler = (_, args) =>
        {
            if (new AssemblyName(args.Name).Name == assemblyName) throw original;
            return null;
        };
        /*
         * 使用唯一程序集名限定进程级回调，并在 finally 中退订，避免干扰其他测试的解析。
         */
        AppDomain.CurrentDomain.AssemblyResolve += handler;
        try
        {
            var failure = Assert.ThrowsAny<Exception>(() => ReflectionInvokeUtils.FindType(typeName));
            Assert.Same(original, failure.GetBaseException());
            Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(typeName, "Run", out var diagnostic));
            Assert.NotNull(diagnostic);
            Assert.Equal(failure.GetType(), diagnostic.GetType());
            Assert.Same(original, diagnostic.GetBaseException());
            Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(typeName, "Run"));
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
        }
    }

    [Theory]
    [InlineData(nameof(ReflectionTarget.Run))]
    [InlineData(nameof(ReflectionTarget.ReturnFalse))]
    [InlineData(nameof(ReflectionTarget.ReturnNull))]
    public void Successful_invocation_reports_reflection_success_and_discards_return_value(string method)
    {
        ReflectionTarget.Calls = 0;
        Assert.True(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, method, out var exception));
        Assert.Null(exception);
        Assert.Equal(1, ReflectionTarget.Calls);
        Assert.True(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, method));
        Assert.Equal(2, ReflectionTarget.Calls);
    }

    [Theory]
    [InlineData("MissingMethod")]
    [InlineData("run")]
    [InlineData("PrivateMethod")]
    [InlineData(nameof(ReflectionTarget.InstanceMethod))]
    public void Missing_or_ineligible_method_returns_false_without_an_exception(string method)
    {
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, method, out var exception));
        Assert.Null(exception);
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, method));
    }

    [Fact]
    public void Missing_type_short_circuits_before_invalid_method_name_is_inspected()
    {
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod("AbilityKit.Missing.Type", null!, out var exception));
        Assert.Null(exception);
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod("AbilityKit.Missing.Type", null!));
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData(nameof(ReflectionTarget.Overloaded), typeof(AmbiguousMatchException))]
    [InlineData(nameof(ReflectionTarget.RequiredArgument), typeof(TargetParameterCountException))]
    [InlineData(nameof(ReflectionTarget.OptionalArgument), typeof(TargetParameterCountException))]
    [InlineData(nameof(ReflectionTarget.ParamsArgument), typeof(TargetParameterCountException))]
    [InlineData(nameof(ReflectionTarget.GenericMethod), typeof(InvalidOperationException))]
    public void Invocation_reports_actual_lookup_and_signature_failures(string? method, Type expected)
    {
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, method!, out var exception));
        Assert.IsType(expected, exception);
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, method!));
    }

    [Fact]
    public void Target_failure_keeps_reflection_wrapper_and_original_inner_exception()
    {
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, nameof(ReflectionTarget.Throw), out var exception));
        var wrapper = Assert.IsType<TargetInvocationException>(exception);
        Assert.Same(ReflectionTarget.Failure, wrapper.InnerException);
        Assert.False(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, nameof(ReflectionTarget.Throw)));
    }

    [Fact]
    public void Task_return_is_not_awaited_and_fault_does_not_change_invocation_result()
    {
        Assert.True(ReflectionInvokeUtils.TryInvokeStaticMethod(TargetName, nameof(ReflectionTarget.FaultedTask), out var exception));
        Assert.Null(exception);
        var task = Assert.IsAssignableFrom<Task>(ReflectionTarget.LastTask);
        Assert.True(task.IsFaulted);
        Assert.Same(ReflectionTarget.Failure, Assert.Single(task.Exception!.InnerExceptions));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Null_disposable_is_a_no_op_for_all_overloads(bool generic, bool withHandler)
    {
        var box = new ReferenceBox(null);
        InvokeDispose(box, generic, withHandler, _ => throw new Exception("不应回调"));
        Assert.Null(box.Read(generic));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Successful_disposal_clears_only_selected_reference_and_subsequent_call_is_empty(bool generic, bool withHandler)
    {
        var probe = new DisposableProbe();
        var box = new ReferenceBox(probe);
        InvokeDispose(box, generic, withHandler, _ => throw new Exception("不应回调"));
        InvokeDispose(box, generic, withHandler);
        Assert.Null(box.Read(generic));
        Assert.Same(probe, box.Read(!generic));
        Assert.Equal(1, probe.Calls);
        probe.Dispose();
        Assert.Equal(2, probe.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Interface_overloads_dispose_the_box_without_mutating_original_struct_copy(bool withHandler)
    {
        var original = new ValueDisposable();
        IDisposable? boxed = original;
        var alias = boxed;
        if (withHandler) DisposeUtils.TryDispose(ref boxed, _ => throw new Exception("不应回调"));
        else DisposeUtils.TryDispose(ref boxed);
        Assert.Null(boxed);
        Assert.Equal(0, original.Calls);
        Assert.Equal(1, Assert.IsType<ValueDisposable>(alias).Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Overload_without_handler_propagates_same_disposal_exception_and_clears_reference(bool generic)
    {
        var original = new InvalidOperationException("释放失败");
        var probe = new DisposableProbe { DuringDispose = () => throw original };
        var box = new ReferenceBox(probe);
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => InvokeDispose(box, generic, false)));
        Assert.Null(box.Read(generic));
        Assert.Equal(1, probe.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Handler_receives_original_exception_before_reference_is_cleared(bool generic)
    {
        var original = new InvalidOperationException("释放失败");
        var probe = new DisposableProbe { DuringDispose = () => throw original };
        var box = new ReferenceBox(probe);
        var calls = 0;
        InvokeDispose(box, generic, true, error =>
        {
            Assert.Same(original, error);
            Assert.Same(probe, box.Read(generic));
            calls++;
        });
        Assert.Equal(1, calls);
        Assert.Null(box.Read(generic));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Null_handler_swallows_disposal_failure_but_still_clears_reference(bool generic)
    {
        var probe = new DisposableProbe { DuringDispose = () => throw new InvalidOperationException("释放失败") };
        var box = new ReferenceBox(probe);
        InvokeDispose(box, generic, true, null);
        Assert.Null(box.Read(generic));
        Assert.Equal(1, probe.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Handler_failure_replaces_disposal_failure_without_preventing_reference_cleanup(bool generic)
    {
        var original = new InvalidOperationException("释放失败");
        var later = new ArgumentException("回调失败");
        var probe = new DisposableProbe { DuringDispose = () => throw original };
        var box = new ReferenceBox(probe);
        var observed = Assert.Throws<ArgumentException>(() => InvokeDispose(box, generic, true, error =>
        {
            Assert.Same(original, error);
            throw later;
        }));
        Assert.Same(later, observed);
        Assert.Null(observed.InnerException);
        Assert.Null(box.Read(generic));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Reference_remains_visible_during_dispose_and_allows_reentrant_disposal(bool generic, bool withHandler)
    {
        var probe = new DisposableProbe();
        var box = new ReferenceBox(probe);
        probe.DuringDispose = () =>
        {
            Assert.Same(probe, box.Read(generic));
            if (probe.Calls == 1) InvokeDispose(box, generic, withHandler);
        };
        InvokeDispose(box, generic, withHandler);
        Assert.Equal(2, probe.Calls);
        Assert.Null(box.Read(generic));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Replacement_written_during_disposal_is_cleared_without_being_disposed(bool generic, bool withHandler)
    {
        var replacement = new DisposableProbe();
        var probe = new DisposableProbe();
        var box = new ReferenceBox(probe);
        probe.DuringDispose = () => box.Write(generic, replacement);
        InvokeDispose(box, generic, withHandler);
        Assert.Null(box.Read(generic));
        Assert.Equal(1, probe.Calls);
        Assert.Equal(0, replacement.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replacement_written_by_exception_handler_is_cleared_even_if_handler_throws(bool generic)
    {
        var replacement = new DisposableProbe();
        var probe = new DisposableProbe { DuringDispose = () => throw new InvalidOperationException("释放失败") };
        var box = new ReferenceBox(probe);
        var later = new ArgumentException("回调失败");
        Assert.Same(later, Assert.Throws<ArgumentException>(() => InvokeDispose(box, generic, true, _ =>
        {
            box.Write(generic, replacement);
            throw later;
        })));
        Assert.Null(box.Read(generic));
        Assert.Equal(0, replacement.Calls);
    }

    private static string TargetName => typeof(ReflectionTarget).AssemblyQualifiedName!;

    private static void InvokeDispose(ReferenceBox box, bool generic, bool withHandler, Action<Exception>? handler = null)
    {
        if (generic)
        {
            if (withHandler) DisposeUtils.TryDispose<DisposableProbe>(ref box.Typed, handler!);
            else DisposeUtils.TryDispose<DisposableProbe>(ref box.Typed);
        }
        else
        {
            if (withHandler) DisposeUtils.TryDispose(ref box.Untyped, handler!);
            else DisposeUtils.TryDispose(ref box.Untyped);
        }
    }

    private sealed class ReferenceBox(DisposableProbe? probe)
    {
        public DisposableProbe? Typed = probe;
        public IDisposable? Untyped = probe;
        public IDisposable? Read(bool generic) => generic ? Typed : Untyped;
        public void Write(bool generic, DisposableProbe? value) { if (generic) Typed = value; else Untyped = value; }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public int Calls { get; private set; }
        public Action? DuringDispose { get; set; }
        public void Dispose() { Calls++; DuringDispose?.Invoke(); }
    }

    private struct ValueDisposable : IDisposable
    {
        public int Calls;
        public void Dispose() => Calls++;
    }

    private sealed class ReflectionTarget
    {
        public static readonly Exception Failure = new InvalidOperationException("目标方法失败");
        public static int Calls;
        public static Task? LastTask;
        public static void Run() => Calls++;
        public static bool ReturnFalse() { Calls++; return false; }
        public static object? ReturnNull() { Calls++; return null; }
        private static void PrivateMethod() => throw new Exception("不应调用");
        public void InstanceMethod() => throw new Exception("不应调用");
        public static void Overloaded() { }
        public static void Overloaded(int value) { }
        public static void RequiredArgument(int value) { }
        public static void OptionalArgument(int value = 1) { }
        public static void ParamsArgument(params int[] values) { }
        public static void GenericMethod<T>() { }
        public static void Throw() => throw Failure;
        public static Task FaultedTask() => LastTask = Task.FromException(Failure);
    }
}
#pragma warning restore CS0618
