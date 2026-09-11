#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Bootstrap
{
    /// <summary>
    /// 世界蓝图，定义游戏世界的结构配置。
    /// </summary>
    public sealed class WorldBlueprint
    {
        private readonly Dictionary<string, IWorldModule> _modules;
        private readonly Dictionary<Type, object> _services;

        public WorldBlueprint()
        {
            _modules = new Dictionary<string, IWorldModule>();
            _services = new Dictionary<Type, object>();
        }

        /// <summary>
        /// 注册模块。
        /// </summary>
        public void RegisterModule(IWorldModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            _modules[module.ModuleId] = module;
        }

        /// <summary>
        /// 获取模块。
        /// </summary>
        public T? GetModule<T>(string moduleId) where T : class, IWorldModule
        {
            return _modules.TryGetValue(moduleId, out var module) ? module as T : null;
        }

        /// <summary>
        /// 获取所有已注册的模块。
        /// </summary>
        public IReadOnlyDictionary<string, IWorldModule> GetAllModules()
        {
            return _modules;
        }

        /// <summary>
        /// 注册服务。
        /// </summary>
        public void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        /// <summary>
        /// 获取服务。
        /// </summary>
        public T? GetService<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var service) ? service as T : null;
        }

        /// <summary>
        /// 初始化所有模块。
        /// </summary>
        public void InitializeModules()
        {
            foreach (var module in _modules.Values)
            {
                module.Initialize();
            }
        }

        /// <summary>
        /// 销毁所有模块。
        /// </summary>
        public void DestroyModules()
        {
            foreach (var module in _modules.Values)
            {
                module.Destroy();
            }

            _modules.Clear();
            _services.Clear();
        }
    }
}
