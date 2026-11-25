using System;

namespace AbilityKit.Network.Abstractions
{
    /// <summary>
    /// 在消费方线程调度工作。内联分发在调用方线程执行；Unity 消费方必须提供在 Unity 主线程捕获的分发器。延迟动作捕获的可变载荷由调用方负责。
    /// </summary>
    public interface IDispatcher
    {
        void Post(Action action);
    }
}
