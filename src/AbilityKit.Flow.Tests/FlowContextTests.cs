using System;
using AbilityKit.Ability.Flow;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class FlowContextTests
{
    private sealed class MarkerA { }
    private sealed class MarkerB { }

    [Fact]
    public void Set_then_TryGet_returns_value_from_root()
    {
        var ctx = new FlowContext();
        var marker = new MarkerA();

        ctx.Set(marker);

        Assert.True(ctx.TryGet<MarkerA>(out var got));
        Assert.Same(marker, got);
    }

    [Fact]
    public void TryGet_missing_returns_false_and_default()
    {
        var ctx = new FlowContext();

        Assert.False(ctx.TryGet<MarkerA>(out var got));
        Assert.Null(got);
    }

    [Fact]
    public void Get_missing_throws_InvalidOperationException()
    {
        var ctx = new FlowContext();

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.Get<MarkerA>());
        Assert.Contains(nameof(MarkerA), ex.Message);
    }

    [Fact]
    public void Get_returns_last_set_value()
    {
        var ctx = new FlowContext();
        var first = new MarkerA();
        var second = new MarkerA();

        ctx.Set(first);
        ctx.Set(second);

        Assert.Same(second, ctx.Get<MarkerA>());
    }

    [Fact]
    public void Remove_deletes_root_value()
    {
        var ctx = new FlowContext();
        ctx.Set(new MarkerA());

        ctx.Remove<MarkerA>();

        Assert.False(ctx.TryGet<MarkerA>(out _));
    }

    [Fact]
    public void Set_writes_into_innermost_open_scope()
    {
        var ctx = new FlowContext();

        using (ctx.BeginScope())
        {
            ctx.Set(new MarkerA());

            Assert.True(ctx.TryGet<MarkerA>(out _));
        }

        // scope 关闭后，写入 scope 的值不应出现在根 map。
        Assert.False(ctx.TryGet<MarkerA>(out _));
    }

    [Fact]
    public void Scope_set_shadows_outer_value()
    {
        var ctx = new FlowContext();
        var outer = new MarkerA();
        ctx.Set(outer);

        using (ctx.BeginScope())
        {
            var inner = new MarkerA();
            ctx.Set(inner);

            Assert.Same(inner, ctx.Get<MarkerA>());
        }
    }

    [Fact]
    public void Scope_dispose_restores_outer_value()
    {
        var ctx = new FlowContext();
        var outer = new MarkerA();
        ctx.Set(outer);

        using (ctx.BeginScope())
        {
            ctx.Set(new MarkerA());
        }

        Assert.Same(outer, ctx.Get<MarkerA>());
    }

    [Fact]
    public void TryGet_searches_innermost_scope_before_outer_and_root()
    {
        var ctx = new FlowContext();
        var rootValue = new MarkerA();
        var midValue = new MarkerA();
        var innerValue = new MarkerA();
        ctx.Set(rootValue);

        using (ctx.BeginScope())
        {
            ctx.Set(midValue);
            using (ctx.BeginScope())
            {
                ctx.Set(innerValue);
                Assert.Same(innerValue, ctx.Get<MarkerA>());
            }

            Assert.Same(midValue, ctx.Get<MarkerA>());
        }

        Assert.Same(rootValue, ctx.Get<MarkerA>());
    }

    [Fact]
    public void Remove_only_removes_from_innermost_scope()
    {
        // 已知语义：Remove 只作用于最内层 scope；外层同类型值保持可见。
        var ctx = new FlowContext();
        var outer = new MarkerA();
        ctx.Set(outer);

        using (ctx.BeginScope())
        {
            ctx.Set(new MarkerA());
            ctx.Remove<MarkerA>();

            // 内层 scope 中已删除，应查到外层值。
            Assert.Same(outer, ctx.Get<MarkerA>());
        }

        Assert.Same(outer, ctx.Get<MarkerA>());
    }

    [Fact]
    public void Remove_in_scope_does_not_touch_root_value()
    {
        var ctx = new FlowContext();
        var rootValue = new MarkerA();
        ctx.Set(rootValue);

        using (ctx.BeginScope())
        {
            // 内层没有该 key，Remove 只 Peek 内层，不会下探到根 map。
            ctx.Remove<MarkerA>();

            Assert.True(ctx.TryGet<MarkerA>(out _));
        }

        Assert.Same(rootValue, ctx.Get<MarkerA>());
    }

    [Fact]
    public void Scope_values_of_different_types_coexist()
    {
        var ctx = new FlowContext();
        var a = new MarkerA();
        var b = new MarkerB();

        using (ctx.BeginScope())
        {
            ctx.Set(a);
            ctx.Set(b);

            Assert.Same(a, ctx.Get<MarkerA>());
            Assert.Same(b, ctx.Get<MarkerB>());
        }
    }

    [Fact]
    public void Clear_removes_root_values_and_all_open_scopes()
    {
        var ctx = new FlowContext();
        ctx.Set(new MarkerA());

        var scope = ctx.BeginScope();
        ctx.Set(new MarkerB());

        ctx.Clear();

        Assert.False(ctx.TryGet<MarkerA>(out _));
        Assert.False(ctx.TryGet<MarkerB>(out _));

        // Clear 释放了所有 scope 句柄对应的字典；再 Dispose 手柄只是空操作。
        scope.Dispose();
    }

    [Fact]
    public void ScopeHandle_double_dispose_is_safe()
    {
        var ctx = new FlowContext();
        var scope = ctx.BeginScope();
        ctx.Set(new MarkerA());

        scope.Dispose();
        scope.Dispose();

        Assert.False(ctx.TryGet<MarkerA>(out _));
    }

    [Fact]
    public void Out_of_order_scope_dispose_pops_innermost_scope()
    {
        // 已知语义（钉住）：ScopeHandle 不绑定自己创建的那层 scope，
        // Dispose 永远弹出当前栈顶。先释放外层句柄会连带丢掉内层数据。
        var ctx = new FlowContext();
        var outerScope = ctx.BeginScope();
        var innerScope = ctx.BeginScope();
        ctx.Set(new MarkerA()); // 写入内层（栈顶）scope

        outerScope.Dispose(); // 实际弹出的是内层字典

        Assert.False(ctx.TryGet<MarkerA>(out _));

        innerScope.Dispose(); // 再弹出外层字典，栈清空，不抛错
    }

    [Fact]
    public void Set_null_in_scope_shadows_outer_value()
    {
        // 2026-08-17 修复：显式存入的 null 对引用类型槽是"存在但为 null"，
        // 内层 scope 的 Set(null) 能正常遮蔽外层同类型值。
        var ctx = new FlowContext();
        var outer = new MarkerA();
        ctx.Set(outer);

        using (ctx.BeginScope())
        {
            ctx.Set<MarkerA>(null);

            Assert.True(ctx.TryGet<MarkerA>(out var got));
            Assert.Null(got);
        }

        // scope 释放后恢复外层值。
        Assert.True(ctx.TryGet<MarkerA>(out var restored));
        Assert.Same(outer, restored);
    }

    [Fact]
    public void Set_null_at_root_is_visible_as_null()
    {
        // 2026-08-17 修复：根 map 存 null 后 TryGet 命中并返回 null，而不是"不存在"。
        var ctx = new FlowContext();
        ctx.Set<MarkerA>(null);

        Assert.True(ctx.TryGet<MarkerA>(out var got));
        Assert.Null(got);
        Assert.Null(ctx.Get<MarkerA>());
    }
}
