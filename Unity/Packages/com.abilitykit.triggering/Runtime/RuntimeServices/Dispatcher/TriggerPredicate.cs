namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 使用事件参数和当前调度上下文求值，返回条件是否通过。
    /// </summary>
    public delegate bool TriggerPredicate<TArgs>(TArgs args, ITriggerDispatcherContext context)
        where TArgs : class;
}
