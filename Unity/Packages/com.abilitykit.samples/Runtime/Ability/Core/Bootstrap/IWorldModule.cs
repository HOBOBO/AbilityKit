#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Bootstrap
{
    /// <summary>
    /// 世界模块接口。
    /// 定义游戏世界中各个功能模块的生命周期。
    /// </summary>
    public interface IWorldModule
    {
        /// <summary>
        /// 模块的唯一标识符。
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// 模块的显示名称。
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 模块的优先级，数值越小越先初始化。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 模块是否已初始化。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 初始化模块。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 销毁模块。
        /// </summary>
        void Destroy();

        /// <summary>
        /// 获取模块的依赖模块。
        /// </summary>
        IReadOnlyList<string> GetDependencies();
    }
}
