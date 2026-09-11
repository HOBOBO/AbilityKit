#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Action
{
    /// <summary>
    /// 动作注册表，管理所有可用的动作工厂。
    /// </summary>
    public sealed class ActionRegistry
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyArgs =
            new Dictionary<string, object>();

        private readonly Dictionary<string, IActionFactory> _factories = new();
        private readonly Dictionary<string, List<IActionFactory>> _taggedFactories = new();

        /// <summary>
        /// 注册动作工厂。
        /// </summary>
        public void Register(IActionFactory factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factories[factory.FactoryId] = factory;
        }

        /// <summary>
        /// 按标签注册动作工厂。
        /// </summary>
        public void Register(IActionFactory factory, params string[] tags)
        {
            Register(factory);
            foreach (var tag in tags)
            {
                if (!_taggedFactories.TryGetValue(tag, out var list))
                {
                    list = new List<IActionFactory>();
                    _taggedFactories[tag] = list;
                }
                list.Add(factory);
            }
        }

        /// <summary>
        /// 移除动作工厂。
        /// </summary>
        public bool Unregister(string factoryId)
        {
            return _factories.Remove(factoryId);
        }

        /// <summary>
        /// 获取动作工厂。
        /// </summary>
        public IActionFactory GetFactory(string factoryId)
        {
            return _factories.TryGetValue(factoryId, out var factory) ? factory : null;
        }

        /// <summary>
        /// 获取支持指定动作类型的工厂。
        /// </summary>
        public IActionFactory GetFactoryFor(string actionType)
        {
            foreach (var factory in _factories.Values)
            {
                if (factory.CanCreate(actionType))
                    return factory;
            }
            return null;
        }

        /// <summary>
        /// 获取所有支持指定动作类型的工厂。
        /// </summary>
        public IEnumerable<IActionFactory> GetFactoriesFor(string actionType)
        {
            foreach (var factory in _factories.Values)
            {
                if (factory.CanCreate(actionType))
                    yield return factory;
            }
        }

        /// <summary>
        /// 按标签获取动作工厂。
        /// </summary>
        public IEnumerable<IActionFactory> GetFactoriesByTag(string tag)
        {
            if (_taggedFactories.TryGetValue(tag, out var list))
            {
                foreach (var factory in list)
                    yield return factory;
            }
        }

        /// <summary>
        /// 创建动作。
        /// </summary>
        public IAction Create(string actionType, IReadOnlyDictionary<string, object> args = null)
        {
            var factory = GetFactoryFor(actionType);
            if (factory == null)
                throw new InvalidOperationException($"No factory found for action type: {actionType}");

            return factory.Create(actionType, args ?? EmptyArgs);
        }

        /// <summary>
        /// 尝试创建动作。
        /// </summary>
        public bool TryCreate(string actionType, IReadOnlyDictionary<string, object> args, out IAction action)
        {
            var factory = GetFactoryFor(actionType);
            if (factory == null)
            {
                action = null;
                return false;
            }

            action = factory.Create(actionType, args ?? EmptyArgs);
            return true;
        }

        /// <summary>
        /// 从规格创建动作。
        /// </summary>
        public IAction CreateFromSpec(IActionSpec spec)
        {
            return spec.ActionType == null ? throw new InvalidOperationException("Spec has no action type") :
                   Create(spec.ActionType, spec.Args);
        }
    }
}
