#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Action
{
    /// <summary>
    /// 动作工厂接口，用于根据配置创建动作实例。
    /// </summary>
    public interface IActionFactory
    {
        /// <summary>
        /// 工厂的唯一标识符。
        /// </summary>
        string FactoryId { get; }

        /// <summary>
        /// 是否支持创建指定类型的动作。
        /// </summary>
        bool CanCreate(string actionType);

        /// <summary>
        /// 创建动作实例。
        /// </summary>
        IAction Create(string actionType, IReadOnlyDictionary<string, object> args);
    }

    /// <summary>
    /// 动作规格接口，表示动作的配置数据。
    /// 规格会在运行时被解析为具体的动作实例。
    /// </summary>
    public interface IActionSpec
    {
        /// <summary>
        /// 动作类型。
        /// </summary>
        string ActionType { get; }

        /// <summary>
        /// 动作参数。
        /// </summary>
        IReadOnlyDictionary<string, object> Args { get; }

        /// <summary>
        /// 使用指定的工厂创建动作。
        /// </summary>
        IAction CreateAction(IActionFactory factory);
    }

    /// <summary>
    /// 动作规格基类，提供通用的规格实现。
    /// </summary>
    public abstract class ActionSpecBase : IActionSpec
    {
        public abstract string ActionType { get; }
        public IReadOnlyDictionary<string, object> Args { get; }

        protected ActionSpecBase(IReadOnlyDictionary<string, object> args = null)
        {
            Args = args ?? EmptyArgs;
        }

        public abstract IAction CreateAction(IActionFactory factory);

        protected static readonly IReadOnlyDictionary<string, object> EmptyArgs =
            new System.Collections.Generic.Dictionary<string, object>();
    }

    /// <summary>
    /// 动作规格的解析器接口。
    /// 用于将配置数据（如 JSON）解析为动作规格。
    /// </summary>
    public interface IActionSpecParser
    {
        /// <summary>
        /// 解析动作规格。
        /// </summary>
        IActionSpec Parse(string actionType, IReadOnlyDictionary<string, object> rawArgs);

        /// <summary>
        /// 解析动作规格列表。
        /// </summary>
        IActionSpec[] ParseAll(IReadOnlyDictionary<string, object>[] rawSpecs);
    }
}
