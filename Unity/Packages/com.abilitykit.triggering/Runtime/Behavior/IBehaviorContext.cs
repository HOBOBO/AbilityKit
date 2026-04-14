using AbilityKit.Triggering.Runtime.Config.Values;

namespace AbilityKit.Triggering.Runtime.Behavior
{
    /// <summary>
    /// 行为上下文接口
    /// 提供运行时执行所需的数据访问
    /// </summary>
    public interface IBehaviorContext
    {
        /// <summary>
        /// 事件载荷参数
        /// </summary>
        object Args { get; }

        /// <summary>
        /// 黑板数据访问器
        /// </summary>
        IBlackboardResolver Blackboards { get; }

        /// <summary>
        /// Action 注册表
        /// </summary>
        IActionRegistry Actions { get; }

        /// <summary>
        /// 数值解析器
        /// </summary>
        IValueResolver Values { get; }
    }

    /// <summary>
    /// 黑板数据访问器接口
    /// 运行时动态数据存储
    /// </summary>
    public interface IBlackboardResolver
    {
        bool TryGetValue<T>(int boardId, string key, out T value);
        void SetValue<T>(int boardId, string key, T value);
    }

    /// <summary>
    /// Action 注册表接口
    /// </summary>
    public interface IActionRegistry
    {
        bool TryGet<T>(object actionId, out T action, out string error);
        bool TryGet(object actionId, System.Type type, out object action, out string error);
    }

    /// <summary>
    /// 数值解析器接口
    /// 将 ValueRefConfig 解析为运行时数值
    /// </summary>
    public interface IValueResolver
    {
        double Resolve(IValueRefConfig valueRef, IBehaviorContext context);
    }
}