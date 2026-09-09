namespace AbilityKit.BehaviorTree.Execution
{
    public enum LifecycleExceptionPolicy
    {
        /// <summary>完成必要清理后，将原始生命周期异常抛给宿主。</summary>
        Throw = 0,
        /// <summary>记录但不向宿主抛出；Start/Tick 异常会停止当前树，Stop 异常不阻断其余清理。</summary>
        CaptureAndContinue = 1,
    }
}
