using System;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// Trigger 相关的委托定义
    /// Predicate 和 Action 统一使用 NamedArgsDict 作为参数容器
    /// </summary>
    public delegate bool Predicate0<TArgs, TCtx>(TArgs args, ExecCtx<TCtx> ctx);
    public delegate bool Predicate1<TArgs, TCtx>(TArgs args, NamedArgsDict args_, ExecCtx<TCtx> ctx);
    public delegate bool Predicate2<TArgs, TCtx>(TArgs args, NamedArgsDict args_, ExecCtx<TCtx> ctx);

    public delegate void Action0<TArgs, TCtx>(TArgs args, ExecCtx<TCtx> ctx);
    public delegate void Action1<TArgs, TCtx>(TArgs args, NamedArgsDict args_, ExecCtx<TCtx> ctx);
    public delegate void Action2<TArgs, TCtx>(TArgs args, NamedArgsDict args_, ExecCtx<TCtx> ctx);
}
