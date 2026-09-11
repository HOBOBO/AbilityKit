#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Action
{
    /// <summary>
    /// 动作执行上下文，包含执行动作所需的所有信息。
    /// </summary>
    public interface IActionContext
    {
        /// <summary>
        /// 动作执行器，用于获取服务和资源。
        /// </summary>
        IActionExecutor Executor { get; }

        /// <summary>
        /// 动作来源，通常是触发动作的实体或角色。
        /// </summary>
        object Source { get; }

        /// <summary>
        /// 动作目标。
        /// </summary>
        object Target { get; }

        /// <summary>
        /// 动作参数。
        /// </summary>
        IReadOnlyDictionary<string, object> Args { get; }

        /// <summary>
        /// 获取参数值。
        /// </summary>
        T GetArg<T>(string key, T defaultValue = default);

        /// <summary>
        /// 设置上下文数据。
        /// </summary>
        void SetData(string key, object value);

        /// <summary>
        /// 获取上下文数据。
        /// </summary>
        T GetData<T>(string key);

        /// <summary>
        /// 获取当前执行时间（毫秒）。
        /// </summary>
        long ElapsedMs { get; }
    }

    /// <summary>
    /// 动作执行器，提供执行动作所需的服务和资源。
    /// </summary>
    public interface IActionExecutor
    {
        /// <summary>
        /// 获取服务。
        /// </summary>
        T GetService<T>() where T : class;
    }

    /// <summary>
    /// 动作接口，表示一个可执行的动作。
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// 动作的唯一标识符。
        /// </summary>
        string ActionId { get; }

        /// <summary>
        /// 动作的显示名称。
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 执行动作。
        /// </summary>
        ActionResult Execute(IActionContext context);

        /// <summary>
        /// 尝试取消动作。
        /// </summary>
        bool TryCancel();
    }

    /// <summary>
    /// 动作执行结果。
    /// </summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public object Data { get; }

        public static ActionResult Succeeded(object data = null) => new(true, null, data);
        public static ActionResult Failed(string error) => new(false, error, null);

        private ActionResult(bool success, string error, object data)
        {
            Success = success;
            ErrorMessage = error;
            Data = data;
        }
    }
}
