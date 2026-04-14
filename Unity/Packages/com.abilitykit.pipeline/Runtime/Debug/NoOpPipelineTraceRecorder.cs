namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线追踪记录器 Runtime NoOp 实现
    /// 不记录任何追踪数据，节省性能
    /// </summary>
    public sealed class NoOpPipelineTraceRecorder : IPipelineTraceRecorder
    {
        public static readonly NoOpPipelineTraceRecorder Instance = new NoOpPipelineTraceRecorder();

        public bool IsEnabled => false;

        public void Record(IPipelineLifeOwner owner, PipelineTraceData data)
        {
        }

        public IPipelineRunTrace GetTrace(int ownerId)
        {
            return null;
        }
    }

    /// <summary>
    /// 管线追踪记录 Runtime NoOp 实现
    /// </summary>
    public sealed class NoOpPipelineRunTrace : IPipelineRunTrace
    {
        public static readonly NoOpPipelineRunTrace Instance = new NoOpPipelineRunTrace();

        public int Capacity => 0;
        public int Count => 0;

        public void Add(EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message)
        {
        }
    }
}
