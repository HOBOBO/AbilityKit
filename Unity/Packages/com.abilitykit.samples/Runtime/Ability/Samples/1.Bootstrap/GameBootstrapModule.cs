#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Logic.Ability.Core.Bootstrap;

namespace AbilityKit.Samples.Logic.Ability.Samples.Bootstrap
{
    /// <summary>
    /// 游戏启动模块的示例实现。
    /// 展示如何实现 IWorldModule 接口。
    /// </summary>
    public class GameBootstrapModule : IWorldModule
    {
        private readonly string _moduleId;
        private readonly string _displayName;
        private readonly int _priority;
        private bool _isInitialized;

        public GameBootstrapModule(string moduleId, string displayName, int priority)
        {
            _moduleId = moduleId;
            _displayName = displayName;
            _priority = priority;
            _isInitialized = false;
        }

        public string ModuleId => _moduleId;

        public string DisplayName => _displayName;

        public int Priority => _priority;

        public bool IsInitialized => _isInitialized;

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            OnInitialize();
            _isInitialized = true;
        }

        public void Destroy()
        {
            if (!_isInitialized)
            {
                return;
            }

            OnDestroy();
            _isInitialized = false;
        }

        public IReadOnlyList<string> GetDependencies()
        {
            return Array.Empty<string>();
        }

        protected virtual void OnInitialize()
        {
            Console.WriteLine($"[{_displayName}] Initializing...");
        }

        /// <summary>
        /// 子类重写的销毁逻辑。
        /// </summary>
        protected virtual void OnDestroy()
        {
            Console.WriteLine($"[{_displayName}] Destroying...");
        }
    }
}
