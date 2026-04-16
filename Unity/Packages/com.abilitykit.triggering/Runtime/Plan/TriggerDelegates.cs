using System;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// Trigger 相关的委托定义
    /// 这些委托原本在 PlannedTrigger 内部，现在提取到公共命名空间
    /// </summary>
    public delegate bool Predicate0<TArgs, TCtx>(TArgs args, ExecCtx<TCtx> ctx) where TArgs : class;
    public delegate bool Predicate1<TArgs, TCtx>(TArgs args, double arg0, ExecCtx<TCtx> ctx) where TArgs : class;
    public delegate bool Predicate2<TArgs, TCtx>(TArgs args, double arg0, double arg1, ExecCtx<TCtx> ctx) where TArgs : class;

    public delegate void Action0<TArgs, TCtx>(TArgs args, ExecCtx<TCtx> ctx) where TArgs : class;
    public delegate void Action1<TArgs, TCtx>(TArgs args, double arg0, ExecCtx<TCtx> ctx) where TArgs : class;
    public delegate void Action2<TArgs, TCtx>(TArgs args, double arg0, double arg1, ExecCtx<TCtx> ctx) where TArgs : class;
}
