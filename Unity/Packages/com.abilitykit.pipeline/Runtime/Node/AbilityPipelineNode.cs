namespace AbilityKit.Ability
{
    /// <summary>
    ///  管线节点基类
    /// </summary>
    public abstract class AbilityPipelineNode : IAbilityPipelineNode
    {
        public string Id { get; }
        // 节点状态
        protected EAbilityPipelineNodeState State { get; set; }
        
        // 核心执行方法
        public virtual IAbilityPipelineNodeExecuteResult Execute(IAbilityPipelineContext context)
        {
            if (State == EAbilityPipelineNodeState.Ready)
            {
                OnEnter(context);
                State = EAbilityPipelineNodeState.Running;
            }
        
            var result = OnExecute(context);
        
            if (result.IsCompleted)
            {
                OnExit(context);
                State = EAbilityPipelineNodeState.Completed;
            }
        
            return result;
        }
    
        protected virtual void OnEnter(IAbilityPipelineContext context) { }
        protected abstract IAbilityPipelineNodeExecuteResult OnExecute(IAbilityPipelineContext context);
        protected virtual void OnExit(IAbilityPipelineContext context) { }
    }
}