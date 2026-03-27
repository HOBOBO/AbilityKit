using System;
using System.Collections.Generic;

namespace UnityHFSM.Actions
{
    /// <summary>
    /// 行为上下文，包含执行所需的所有信息
    /// Core 层版本，不依赖 Unity
    /// </summary>
    public class BehaviorContext
    {
        /// <summary>
        /// 帧间隔时间
        /// </summary>
        public float deltaTime;

        /// <summary>
        /// 行为开始后的总流逝时间
        /// </summary>
        public float elapsedTime;

        /// <summary>
        /// 用于启动协程的宿主（Unity 下为 MonoBehaviour；纯 C# 环境可为 null）
        /// </summary>
        public object mono;

        /// <summary>
        /// 自定义用户数据
        /// </summary>
        public object userData;

        /// <summary>
        /// 当前正在等待的协程 yield 对象
        /// </summary>
        public object currentYield;

        /// <summary>
        /// 状态机引用，用于触发转换等
        /// </summary>
        public object fsm;

        /// <summary>
        /// 行为日志回调
        /// </summary>
        public Action<string> onLog;

        /// <summary>
        /// 变量存储，用于 SetVariable 等行为
        /// </summary>
        private Dictionary<string, object> variables = new Dictionary<string, object>();

        public void SetVariable<T>(string name, T value)
        {
            variables[name] = value;
        }

        public T GetVariable<T>(string name)
        {
            if (variables.TryGetValue(name, out var value))
            {
                return (T)value;
            }
            return default(T);
        }

        public bool HasVariable(string name)
        {
            return variables.ContainsKey(name);
        }
    }
}
