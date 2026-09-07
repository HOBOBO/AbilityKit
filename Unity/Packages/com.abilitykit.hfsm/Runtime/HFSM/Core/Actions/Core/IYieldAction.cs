using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 支持协程的行为基类
    /// </summary>
    public interface IYieldAction : IAction
    {
        /// <summary>
        /// 获取协程枚举器
        /// </summary>
        IEnumerator GetYieldEnumerator(BehaviorContext context);

        /// <summary>
        /// 当前是否正在等待协程完成
        /// </summary>
        bool IsWaitingForCoroutine { get; }
    }
}
