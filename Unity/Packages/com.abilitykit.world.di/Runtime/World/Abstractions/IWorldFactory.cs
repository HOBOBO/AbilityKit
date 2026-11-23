namespace AbilityKit.Ability.World.Abstractions
{
    /// <summary>
    /// 按选项构造世界实例；返回实例前的失败清理由工厂负责。
    /// </summary>
    public interface IWorldFactory
    {
        /// <summary>
        /// 返回尚未初始化、ID 与请求一致的新世界。用于管理器时，成功返回即移交实例的释放责任。
        /// </summary>
        IWorld Create(WorldCreateOptions options);
    }
}
