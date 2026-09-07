namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>
    /// 领域服务解析器：节点通过它获取宿主服务（配置、目标搜索、时间源等）
    /// 使领域节点不依赖任何具体宿主装配。实现由接入方提�?
    /// </summary>
    public interface ServiceResolver
    {
        T Resolve<T>() where T : class;
        bool TryResolve<T>(out T service) where T : class;
    }
}
